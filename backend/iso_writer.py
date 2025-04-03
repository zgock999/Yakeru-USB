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
        
        # 進捗状況追跡用の状態変数
        state = {
            "bytes_written": 0,
            "iso_size": iso_size,
            "start_time": time.time(),
            "last_report_time": time.time()
        }
        
        # Windowsの場合、特別な処理が必要
        if platform.system() == "Windows":
            return _write_iso_to_windows_device(iso_path, device_path, state, progress_callback)
        
        # Linux/macOSの場合の標準処理    
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
                    
                    # 定期的に進捗を報告
                    current_time = time.time()
                    if progress_callback and (current_time - state["last_report_time"]) >= report_interval:
                        progress_percent = min(100, int(state["bytes_written"] * 100 / state["iso_size"]))
                        progress_callback(progress_percent, "writing")
                        state["last_report_time"] = current_time
                
                # 書き込みバッファをフラッシュ
                device.flush()
                os.fsync(device.fileno())
        
        # 完了を通知
        if progress_callback:
            progress_callback(100, "completed")
            
        return True
        
    except Exception as e:
        # エラーを通知
        if progress_callback:
            progress_callback(0, f"error: {str(e)}")
        raise
    
    finally:
        # 必要に応じてデバイスを安全に取り外す処理を追加できます
        pass

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
                    progress_callback(10, "writing")
                
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
                        # 書き込み進捗を10%から95%の範囲で正規化
                        raw_progress = state["bytes_written"] / state["iso_size"]
                        progress_percent = min(95, 10 + math.floor(raw_progress * 85))
                        progress_callback(progress_percent, "writing")
                        state["last_report_time"] = current_time
                
                # 書き込みバッファをフラッシュ
                if progress_callback:
                    progress_callback(99, "flushing")
                    
                ctypes.windll.kernel32.FlushFileBuffers(h_device)
                
            finally:
                # デバイスハンドルを閉じる
                ctypes.windll.kernel32.CloseHandle(h_device)
        
        # 完了通知
        if progress_callback:
            progress_callback(100, "completed")
        
        return True
        
    except Exception as e:
        # エラーを詳細に通知
        error_message = f"Error writing ISO to device: {str(e)}"
        if progress_callback:
            progress_callback(0, f"error: {error_message}")
        else:
            print(error_message)
        raise

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
