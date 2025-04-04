import os
import time
import shutil
import glob
import platform
import ctypes
from ctypes import wintypes
import subprocess
import tempfile

def get_iso_files(iso_dir):
    """指定ディレクトリ内のISOファイル一覧を取得"""
    if not os.path.exists(iso_dir):
        return []
        
    iso_files = []
    for file in glob.glob(os.path.join(iso_dir, "*.iso")):
        filename = os.path.basename(file)
        size = os.path.getsize(file)
        iso_files.append({
            "name": filename,
            "size": size,
            "size_formatted": format_size(size),
            "path": file
        })
    
    return iso_files

def format_size(size_bytes):
    """バイト数を読みやすいフォーマットに変換"""
    for unit in ['B', 'KB', 'MB', 'GB', 'TB']:
        if size_bytes < 1024.0:
            return f"{size_bytes:.2f} {unit}"
        size_bytes /= 1024.0
    return f"{size_bytes:.2f} PB"

def write_iso_to_device(iso_path, device_path, progress_callback=None):
    """ISOファイルをブロックデバイスに書き込む"""
    try:
        # 書き込み開始を通知
        if progress_callback:
            progress_callback(0, "started")
            
        # Windowsの場合、特別な処理が必要
        if platform.system() == "Windows":
            return _write_iso_to_windows_device(iso_path, device_path, progress_callback)
        
        # Linux/macOSの場合の処理
        return _write_iso_to_linux_device(iso_path, device_path, progress_callback)
        
    except Exception as e:
        # エラーを通知
        if progress_callback:
            progress_callback(0, f"error: {str(e)}")
        raise
    
    finally:
        # 必要に応じてデバイスを安全に取り外す処理を追加できます
        pass

def _write_iso_to_linux_device(iso_path, device_path, progress_callback=None):
    """Linux/macOS環境でISOファイルをデバイスに書き込む"""
    try:
        # デバイス準備（Linuxの場合はマウント解除が必要な場合がある）
        if platform.system() == "Linux":
            if progress_callback:
                progress_callback(0, "preparing_disk")
                
            # デバイスがマウントされているか確認し、マウント解除を試みる
            if not _ensure_device_not_mounted(device_path, progress_callback):
                raise OSError(f"Failed to unmount device {device_path}")
                
            if progress_callback:
                progress_callback(0, "disk_prepared")
            
            # Linux環境では、前回の書き込み後にカーネルがキャッシュを保持している可能性があるため
            # デバイスを再オープンする前に強制的にsyncを実行し、IO状態をリセットする
            try:
                subprocess.run(["sync"], check=True)
                # ブロックデバイスのキャッシュをクリア
                if os.path.exists('/sbin/blockdev'):
                    subprocess.run(["blockdev", "--flushbufs", device_path], check=False)
                # 少し待機して、カーネルがデバイスの状態を更新する時間を確保
                time.sleep(1)
            except Exception as e:
                print(f"Warning: Device sync issue: {e}")
        
        # ISOファイルを開く
        with open(iso_path, 'rb') as iso_file:
            # ISOファイルのサイズを取得
            iso_file.seek(0, os.SEEK_END)
            iso_size = iso_file.tell()
            iso_file.seek(0)
            
            if progress_callback:
                progress_callback(0, "opening_device")
                
            # リトライに関する変数
            max_retries = 10  # 最大リトライ回数
            retry_delay = 2.0  # リトライ間隔（秒）
            
            with open(device_path, 'wb') as device:
                buffer_size = 1024 * 1024  # 1MB
                bytes_written = 0
                last_report_time = time.time()
                report_interval = 0.5  # 進捗報告の間隔 (秒)
                
                if progress_callback:
                    progress_callback(0, "writing")
                
                while True:
                    buffer = iso_file.read(buffer_size)
                    if not buffer:
                        break
                    
                    # 書き込み処理にリトライロジックを追加
                    retry_count = 0
                    write_success = False
                    
                    while retry_count <= max_retries and not write_success:
                        if retry_count > 0:
                            # リトライの場合は少し待機
                            time.sleep(retry_delay)
                            print(f"Retrying write operation (attempt {retry_count}/{max_retries})")
                            if progress_callback:
                                progress_callback(int(bytes_written * 100 / iso_size) if iso_size > 0 else 0,
                                                f"writing (retry {retry_count}/{max_retries})")
                        
                        try:
                            # 書き込み処理
                            device.write(buffer)
                            write_success = True
                        except (IOError, OSError) as e:
                            print(f"Write error: {str(e)}")
                            retry_count += 1
                            if retry_count > max_retries:
                                raise OSError(f"Write failed after {max_retries} retries: {str(e)}")
                    
                    bytes_written += len(buffer)
                    print(f"Bytes written: {bytes_written}/{iso_size} ({bytes_written * 100 / iso_size:.2f}%)")
                    
                    # 定期的に進捗を報告
                    current_time = time.time()
                    if progress_callback and (current_time - last_report_time) >= report_interval:
                        progress_percent = int(bytes_written * 100 / iso_size) if iso_size > 0 else 0
                        progress_callback(progress_percent, "writing")
                        last_report_time = current_time
                
                # 書き込みバッファをフラッシュ
                if progress_callback:
                    progress_callback(100, "flushing")  # 99%でフラッシュ中と表示
                
                device.flush()
                os.fsync(device.fileno())
                
                if progress_callback:
                    progress_callback(100, "syncing")  # ディスクキャッシュ同期
                
                # Linux環境ではsync呼び出しでディスクキャッシュを確実に同期
                if platform.system() == "Linux":
                    subprocess.run(["sync"], check=True)
        
        # 完了を通知
        if progress_callback:
            progress_callback(99, "finalizing")  # 完了前に最終化ステップを追加
            
            # 書き込み完了後、Linux環境ではデバイスファイル記述子がクローズされても
            # カーネルバッファが完全にフラッシュされるまで時間がかかるため
            # syncコマンドを3回実行して確実にディスク同期を行う
            for i in range(3):
                try:
                    subprocess.run(["sync"], check=True)
                    time.sleep(0.5)
                except Exception as e:
                    print(f"Warning: Sync issue on attempt {i+1}: {e}")

            # デバイス状態の更新を明示的にカーネルに要求
            try:
                if os.path.exists('/sbin/hdparm'):
                    subprocess.run(["/sbin/hdparm", "-z", device_path], check=False)
            except Exception as e:
                print(f"Warning: Failed to refresh device: {e}")
                
            progress_callback(100, "completed")
        
        return True
        
    except Exception as e:
        # エラーを通知
        if progress_callback:
            progress_callback(0, f"error: {str(e)}")
        raise

def _ensure_device_not_mounted(device_path, progress_callback=None):
    """デバイスがマウントされていないことを確認（Linuxのみ）"""
    if platform.system() != "Linux":
        return True
    
    try:
        # マウントポイントを確認
        mount_output = subprocess.check_output(["mount"], universal_newlines=True)
        mount_lines = mount_output.splitlines()
        
        # デバイスパーティションを取得（例: /dev/sdb -> /dev/sdb1, /dev/sdb2等）
        device_base = os.path.basename(device_path)
        mounted_partitions = []
        
        for line in mount_lines:
            if device_base in line or device_path in line:
                parts = line.split()
                if len(parts) >= 1:
                    mounted_partitions.append(parts[0])
        
        # マウントされているパーティションがある場合はアンマウント
        if mounted_partitions:
            if progress_callback:
                progress_callback(0, "dismounting_volume")
                
            for partition in mounted_partitions:
                print(f"Unmounting {partition}...")
                # 強制オプションを追加
                subprocess.run(["umount", "-f", partition], check=False)
                
            # アンマウント後に再確認
            time.sleep(1)
            mount_output = subprocess.check_output(["mount"], universal_newlines=True)
            for partition in mounted_partitions:
                if partition in mount_output:
                    print(f"Failed to unmount {partition}")
                    # 最後の手段: lazily unmountを試行
                    try:
                        subprocess.run(["umount", "-l", partition], check=False)
                        time.sleep(0.5)
                    except Exception as e:
                        print(f"Error during lazy unmount: {e}")
            
            # 再確認
            time.sleep(0.5)
            mount_output = subprocess.check_output(["mount"], universal_newlines=True)
            still_mounted = False
            for partition in mounted_partitions:
                if partition in mount_output:
                    still_mounted = True
                    print(f"Partition {partition} is still mounted")
            
            # それでも問題がある場合は続行するが警告を出す
            if still_mounted:
                print("WARNING: Some partitions could not be unmounted. Continuing anyway...")
        
        return True
        
    except Exception as e:
        print(f"Error ensuring device not mounted: {e}")
        return False

def _write_iso_to_windows_device(iso_path, device_path, progress_callback=None):
    """Windows環境でISOファイルをデバイスに書き込む"""
    autoplay_enabled = None  # 自動再生の設定を保持する変数を初期化
    
    try:
        # デバイス番号を抽出
        if '\\\\?\\PhysicalDrive' in device_path:
            drive_number = device_path.split('PhysicalDrive')[-1]
        elif '\\\\.\\PhysicalDrive' in device_path:
            drive_number = device_path.split('PhysicalDrive')[-1]
        else:
            raise ValueError(f"Invalid device path format: {device_path}")
        
        # Windows自動再生を一時的に無効化
        if progress_callback:
            progress_callback(0, "disabling_autoplay")
        
        # 自動再生の設定を保存して無効化
        autoplay_enabled = _disable_windows_autoplay()
        
        # DiskPartスクリプトを使用してディスクをクリーンアップ
        if not _prepare_disk_with_diskpart(drive_number, progress_callback):
            # エラー時の処理は上位のエラーハンドリングで行うため、ここでは設定を復元せずにエラーを上げる
            raise OSError(f"Failed to prepare disk {drive_number}")
        
        if progress_callback:
            progress_callback(0, "opening_device")
        
        # 正規化されたデバイスパス
        if not device_path.startswith("\\\\.\\"):
            normalized_path = f"\\\\.\\PhysicalDrive{drive_number}"
        else:
            normalized_path = device_path
        
        # Windows APIを直接使用してディスクに書き込む
        GENERIC_WRITE = 0x40000000
        FILE_SHARE_READ = 0x00000001
        FILE_SHARE_WRITE = 0x00000002
        OPEN_EXISTING = 3
        INVALID_HANDLE_VALUE = wintypes.HANDLE(-1).value
        
        # ISOファイルを開く
        with open(iso_path, 'rb') as iso_file:
            # ファイルサイズをseekを使って取得
            iso_file.seek(0, os.SEEK_END)
            iso_size = iso_file.tell()
            iso_file.seek(0)  # ファイルポインタを先頭に戻す
            print(f"ISO size: {iso_size} bytes")
            
            # CreateFileWでデバイスを開く
            h_device = ctypes.windll.kernel32.CreateFileW(
                normalized_path,                   # デバイスパス
                GENERIC_WRITE,                     # アクセスモード（書き込み）
                FILE_SHARE_READ | FILE_SHARE_WRITE,# 共有モード
                None,                              # セキュリティ属性
                OPEN_EXISTING,                     # 作成方法
                0,                                 # フラグと属性
                None                               # テンプレートファイル
            )
            
            if h_device == INVALID_HANDLE_VALUE:
                error_code = ctypes.windll.kernel32.GetLastError()
                # ここでは設定を復元せず、後のfinallyブロックでリソース解放を一括で行う
                raise OSError(f"Failed to open device. Error code: {error_code}")
            
            try:
                # 書き込みを開始
                if progress_callback:
                    progress_callback(0, "writing")
                
                buffer_size = 1024 * 1024  # 1MB
                bytes_written = 0
                last_report_time = time.time()
                report_interval = 0.5  # 進捗報告の間隔 (秒)
                
                # リトライに関する変数
                max_retries = 10  # 最大リトライ回数を3回から10回に増やす
                retry_delay = 2.0  # リトライ間隔を1.0秒から2.0秒に増やす
                
                while True:
                    buffer = iso_file.read(buffer_size)
                    if not buffer:
                        break
                    
                    # バッファの長さ
                    buffer_len = len(buffer)
                    
                    # バッファをctypes.c_char配列に変換
                    c_buffer = (ctypes.c_char * buffer_len).from_buffer_copy(buffer)
                    bytes_to_write = wintypes.DWORD(buffer_len)
                    bytes_written_ptr = wintypes.DWORD(0)
                    
                    # 書き込み処理にリトライロジックを追加
                    retry_count = 0
                    write_success = False
                    last_error_code = 0
                    
                    while retry_count <= max_retries and not write_success:
                        if retry_count > 0:
                            # リトライの場合は少し待機
                            time.sleep(retry_delay)
                            print(f"Retrying write operation (attempt {retry_count}/{max_retries})")
                            if progress_callback:
                                progress_callback(int(bytes_written * 100 / iso_size) if iso_size > 0 else 0, 
                                                 f"writing (retry {retry_count}/{max_retries})")
                        
                        # WriteFileでデバイスに書き込み
                        success = ctypes.windll.kernel32.WriteFile(
                            h_device,                  # デバイスハンドル
                            c_buffer,                  # データバッファ
                            bytes_to_write,            # 書き込むバイト数
                            ctypes.byref(bytes_written_ptr), # 書き込まれたバイト数
                            None                       # オーバーラップド構造体
                        )
                        
                        if success:
                            write_success = True
                        else:
                            # エラーコードを取得
                            last_error_code = ctypes.windll.kernel32.GetLastError()
                            # エラーコード1のみリトライ（それ以外はリトライしない）
                            if last_error_code != 1:
                                break
                            retry_count += 1
                    
                    # すべてのリトライが失敗した場合
                    if not write_success:
                        if last_error_code == 1:
                            error_message = f"Write failed after {max_retries} retries. Error code: 1 (Write protected media or permission issue)"
                        else:
                            error_message = f"Write failed. Error code: {last_error_code}"
                        
                        # ログに出力してからエラーを投げる
                        print(error_message)
                        raise OSError(error_message)
                    
                    bytes_written += bytes_written_ptr.value
                    print(f"Bytes written: {bytes_written}/{iso_size} ({bytes_written * 100 / iso_size:.2f}%)")
                    
                    # 進捗報告
                    current_time = time.time()
                    if progress_callback and (current_time - last_report_time) >= report_interval:
                        progress_percent = int(bytes_written * 100 / iso_size) if iso_size > 0 else 0
                        progress_callback(progress_percent, "writing")
                        last_report_time = current_time
                
                # 書き込みバッファをフラッシュ
                if progress_callback:
                    progress_callback(99, "flushing")  # 99%でフラッシュ中と表示
                
                flush_success = ctypes.windll.kernel32.FlushFileBuffers(h_device)
                if not flush_success:
                    flush_error = ctypes.windll.kernel32.GetLastError()
                    print(f"Warning: FlushFileBuffers returned error: {flush_error}")
                
            finally:
                # デバイスハンドルを閉じる - 複数回呼ばれないようにfinallyブロックに移動
                ctypes.windll.kernel32.CloseHandle(h_device)
                # ここでは自動再生設定を復元しない
        
        # 書き込みが完了してISO処理が終わってから設定を元に戻す
        if autoplay_enabled is not None:
            print("書き込み完了後に自動再生の設定を復元します")
            _restore_windows_autoplay(autoplay_enabled)
            # 復元後に参照をクリアして複数回呼び出しを防ぐ
            autoplay_enabled = None
        
        # 完了通知
        if progress_callback:
            progress_callback(100, "completed")
        
        return True
        
    except Exception as e:
        # エラーを通知
        if progress_callback:
            progress_callback(0, f"error: {str(e)}")
        raise
    finally:
        # 例外発生時も含めて、ここで必ずautoplayの設定を元に戻す
        if autoplay_enabled is not None:
            print("例外発生時または後処理として自動再生の設定を復元します")
            _restore_windows_autoplay(autoplay_enabled)

def _disable_windows_autoplay():
    """Windows自動再生を一時的に無効化し、元の設定を返す"""
    if platform.system() != "Windows":
        return None  # Windowsでない場合は何もしない
        
    try:
        import winreg
        
        # 現在の自動再生設定を取得（復元用）
        try:
            key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", 0, winreg.KEY_READ)
            current_value = winreg.QueryValueEx(key, "DisableAutoplay")[0]
            winreg.CloseKey(key)
        except:
            current_value = 0  # デフォルトは有効(0)
            
        # 自動再生を無効化
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", 0, winreg.KEY_WRITE)
        winreg.SetValueEx(key, "DisableAutoplay", 0, winreg.REG_DWORD, 1)  # 1=無効化
        winreg.CloseKey(key)
        
        # 設定変更を反映させるため、エクスプローラーに通知
        try:
            ctypes.windll.shell32.SHChangeNotify(0x08000000, 0, None, None)  # SHCNE_ASSOCCHANGED
        except:
            pass
            
        print("Windows Autoplay disabled temporarily")
        return current_value
        
    except Exception as e:
        print(f"Failed to disable Windows Autoplay: {e}")
        return None

def _restore_windows_autoplay(previous_value):
    """Windows自動再生の設定を元に戻す"""
    if platform.system() != "Windows" or previous_value is None:
        return
        
    try:
        import winreg
        
        # 自動再生設定を元に戻す
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", 0, winreg.KEY_WRITE)
        winreg.SetValueEx(key, "DisableAutoplay", 0, winreg.REG_DWORD, previous_value)
        winreg.CloseKey(key)
        
        # 設定変更を反映させるため、エクスプローラーに通知
        try:
            ctypes.windll.shell32.SHChangeNotify(0x08000000, 0, None, None)  # SHCNE_ASSOCCHANGED
        except:
            pass
            
        print(f"Windows Autoplay setting restored to previous state: {previous_value}")
        
    except Exception as e:
        print(f"Failed to restore Windows Autoplay setting: {e}")

def _prepare_disk_with_diskpart(disk_number, progress_callback=None):
    """DiskPartを使用してディスクを準備する"""
    try:
        if progress_callback:
            progress_callback(1, "preparing_disk")
            
        # 一時ファイルにDiskPartスクリプトを作成
        with tempfile.NamedTemporaryFile(delete=False, suffix='.txt', mode='w') as script:
            script.write(f"select disk {disk_number}\n")
            script.write("clean\n")
            script.write("exit\n")
            script_path = script.name
        
        if progress_callback:
            progress_callback(0, "cleaning_disk")
            
        # DiskPartを実行
        process = subprocess.Popen(
            ["diskpart", "/s", script_path],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        stdout, stderr = process.communicate()
        
        # 一時ファイルを削除
        try:
            os.unlink(script_path)
        except:
            pass
        
        if process.returncode != 0:
            print(f"DiskPart error: {stderr}")
            return False
        
        if progress_callback:
            progress_callback(0, "disk_prepared")
            
        # 操作の成功後、少し待機（ディスクが再スキャンされるのを待つ）
        time.sleep(2)
        return True
        
    except Exception as e:
        print(f"Error preparing disk with DiskPart: {e}")
        return False

# テスト実行用
if __name__ == "__main__":
    print("Available ISO files:")
    iso_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "isos")
    isos = get_iso_files(iso_dir)
    for iso in isos:
        print(f"{iso['name']} - {iso['size_formatted']}")
