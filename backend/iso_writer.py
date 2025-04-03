import os
import time
import threading
import shutil
import glob
import platform
import ctypes
from ctypes import wintypes
import subprocess
import tempfile
import math

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
            
        # ISOファイルのサイズを取得
        iso_size = os.path.getsize(iso_path)
        
        # 書き込みの進捗ポイント調整用のパラメータ
        # 実際のディスク書き込みでは通常ファイルシステムキャッシュにより
        # 最初は書き込みが高速に見え、その後ディスクへの実際の書き込みが遅くなる
        progress_phases = [
            {"name": "preparing", "start": 0, "end": 5, "duration": 2},
            {"name": "initial_write", "start": 5, "end": 30, "duration": 10},
            {"name": "main_write", "start": 30, "end": 85, "duration": None}, # 実際の書き込み時間に基づく
            {"name": "flushing", "start": 85, "end": 95, "duration": 5},
            {"name": "finalizing", "start": 95, "end": 99, "duration": 3}
        ]
            
        # 進捗状況追跡用の状態変数
        state = {
            "bytes_written": 0,
            "iso_size": iso_size,
            "start_time": time.time(),
            "last_report_time": time.time(),
            "current_phase": 0,
            "phases": progress_phases
        }
        
        # 非同期で進捗フェーズ更新をシミュレート
        if progress_callback:
            simulate_thread = threading.Thread(
                target=_simulate_progress_phases, 
                args=(state, progress_callback)
            )
            simulate_thread.daemon = True
            simulate_thread.start()
        
        # Windowsの場合、特別な処理が必要
        if platform.system() == "Windows":
            result = _write_iso_to_windows_device(iso_path, device_path, state, progress_callback)
        else:
            # Linux/macOSの場合の標準処理
            result = _write_iso_to_standard_device(iso_path, device_path, state, progress_callback)
        
        # 完了を通知
        if progress_callback and result:
            progress_callback(100, "completed")
            
        return result
        
    except Exception as e:
        # エラーを通知
        if progress_callback:
            progress_callback(0, f"error: {str(e)}")
        raise
    
    finally:
        # 必要に応じてデバイスを安全に取り外す処理を追加できます
        pass

def _simulate_progress_phases(state, callback):
    """進捗フェーズを順次シミュレートして表示を滑らかにする"""
    try:
        phases = state["phases"]
        
        for i, phase in enumerate(phases):
            if phase["duration"] is None:
                continue  # 実際の書き込み状況に基づく実時間フェーズはスキップ
                
            # フェーズの開始
            state["current_phase"] = i
            
            start_percent = phase["start"]
            end_percent = phase["end"]
            duration = phase["duration"]
            
            # フェーズ内の進捗を徐々に更新
            steps = 20  # 更新回数
            step_time = duration / steps
            
            for step in range(steps):
                # 進捗度を計算（0.0～1.0）
                progress_ratio = step / steps
                # 現在のフェーズでの進捗％を計算
                current_percent = start_percent + (end_percent - start_percent) * progress_ratio
                # 丸めて整数に
                percent = int(current_percent)
                
                # コールバックで通知
                callback(percent, phase["name"])
                
                # 少し待機
                time.sleep(step_time)
                
                # 実際の書き込みが終わっていれば中断
                if state.get("write_completed", False):
                    break
    except Exception as e:
        print(f"Error in progress simulation: {e}")

def _write_iso_to_standard_device(iso_path, device_path, state, progress_callback=None):
    """Linux/macOS環境でISOファイルをデバイスに書き込む"""
    try:
        with open(iso_path, 'rb') as iso_file:
            with open(device_path, 'wb') as device:
                buffer_size = 1024 * 1024  # 1MB
                report_interval = 0.5  # 進捗報告の間隔 (秒)
                
                while True:
                    buffer = iso_file.read(buffer_size)
                    if not buffer:
                        break
                        
                    device.write(buffer)
                    state["bytes_written"] += len(buffer)
                    
                    # 定期的に進捗を報告（実際の書き込み進捗）
                    current_time = time.time()
                    if progress_callback and (current_time - state["last_report_time"]) >= report_interval:
                        # メインの書き込みフェーズにマッピング
                        phase = state["phases"][2]  # main_write phase
                        raw_progress = state["bytes_written"] / state["iso_size"]
                        
                        # 30-85%の範囲に正規化
                        progress_percent = int(phase["start"] + raw_progress * (phase["end"] - phase["start"]))
                        progress_callback(progress_percent, "writing")
                        state["last_report_time"] = current_time
                
                # 書き込みバッファをフラッシュ
                device.flush()
                os.fsync(device.fileno())
        
        # 書き込み完了フラグを設定
        state["write_completed"] = True
        
        # フェーズの書き込みが終了したことを通知
        if progress_callback:
            state["current_phase"] = 3  # フラッシュフェーズへ
            progress_callback(state["phases"][3]["start"], "flushing")
        
        return True
    except Exception as e:
        # エラー発生
        state["write_completed"] = True
        raise e

def _write_iso_to_windows_device(iso_path, device_path, state, progress_callback=None):
    """Windows環境でISOファイルをデバイスに書き込む"""
    try:
        # デバイス番号を抽出
        if '\\\\?\\PhysicalDrive' in device_path:
            drive_number = device_path.split('PhysicalDrive')[-1]
        elif '\\\\.\\PhysicalDrive' in device_path:
            drive_number = device_path.split('PhysicalDrive')[-1]
        else:
            raise ValueError(f"Invalid device path format: {device_path}")
        
        # DiskPartスクリプトを使用してディスクをクリーンアップ
        if not _prepare_disk_with_diskpart(drive_number, progress_callback):
            raise OSError(f"Failed to prepare disk {drive_number}")
        
        if progress_callback:
            progress_callback(5, "opening_device")
        
        # 正規化されたデバイスパス
        if not device_path.startswith("\\\\.\\"):
            normalized_path = f"\\\\.\\PhysicalDrive{drive_number}"
        else:
            normalized_path = device_path
        
        # Windows APIを直接使用してディスクに書き込む
        GENERIC_READ = 0x80000000
        GENERIC_WRITE = 0x40000000
        FILE_SHARE_READ = 0x00000001
        FILE_SHARE_WRITE = 0x00000002
        OPEN_EXISTING = 3
        INVALID_HANDLE_VALUE = wintypes.HANDLE(-1).value
        
        # ISOファイルを開く
        with open(iso_path, 'rb') as iso_file:
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
                raise OSError(f"Failed to open device. Error code: {error_code}")
            
            try:
                # 書き込みを開始
                if progress_callback:
                    # 準備フェーズ完了、初期書き込みフェーズに移行
                    state["current_phase"] = 1
                    progress_callback(state["phases"][1]["start"], "initial_write")
                
                buffer_size = 1024 * 1024  # 1MB
                report_interval = 0.5  # 進捗報告の間隔 (秒)
                
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
                    
                    # WriteFileでデバイスに書き込み
                    success = ctypes.windll.kernel32.WriteFile(
                        h_device,                  # デバイスハンドル
                        c_buffer,                  # データバッファ
                        bytes_to_write,            # 書き込むバイト数
                        ctypes.byref(bytes_written_ptr), # 書き込まれたバイト数
                        None                       # オーバーラップド構造体
                    )
                    
                    if not success:
                        error_code = ctypes.windll.kernel32.GetLastError()
                        raise OSError(f"Write failed. Error code: {error_code}")
                    
                    state["bytes_written"] += bytes_written_ptr.value
                    
                    # 進捗報告
                    current_time = time.time()
                    if progress_callback and (current_time - state["last_report_time"]) >= report_interval:
                        # メインの書き込みフェーズにマッピング
                        phase = state["phases"][2]  # main_write phase
                        raw_progress = state["bytes_written"] / state["iso_size"]
                        
                        # 30-85%の範囲に正規化
                        progress_percent = int(phase["start"] + raw_progress * (phase["end"] - phase["start"]))
                        progress_callback(progress_percent, "writing")
                        state["last_report_time"] = current_time
                
                # 書き込み完了フラグを設定
                state["write_completed"] = True
                
                # 書き込みバッファをフラッシュ
                if progress_callback:
                    state["current_phase"] = 3  # フラッシュフェーズへ
                    progress_callback(state["phases"][3]["start"], "flushing")
                    
                ctypes.windll.kernel32.FlushFileBuffers(h_device)
                
                # 最後のフェイズに移行
                if progress_callback:
                    state["current_phase"] = 4  # 最終化フェーズへ
                    progress_callback(state["phases"][4]["start"], "finalizing")
                
            finally:
                # デバイスハンドルを閉じる
                ctypes.windll.kernel32.CloseHandle(h_device)
        
        return True
        
    except Exception as e:
        # エラー発生
        state["write_completed"] = True
        raise e

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
            progress_callback(2, "cleaning_disk")
            
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
            progress_callback(3, "disk_prepared")
            
        # 操作の成功後、少し待機（ディスクが再スキャンされるのを待つ）
        time.sleep(2)
        return True
        
    except Exception as e:
        print(f"Error preparing disk with DiskPart: {e}")
        return False

# 既存の_dismount_windows_volumes関数はもう使用しないため、残してもよいが、
# 上記の_prepare_disk_with_diskpart関数を優先して使用する

# テスト実行用
if __name__ == "__main__":
    print("Available ISO files:")
    iso_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "isos")
    isos = get_iso_files(iso_dir)
    for iso in isos:
        print(f"{iso['name']} - {iso['size_formatted']}")
