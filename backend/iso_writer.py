import os
import time
import threading
import shutil
import glob

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
        
        with open(iso_path, 'rb') as iso_file:
            with open(device_path, 'wb') as device:
                buffer_size = 1024 * 1024  # 1MB
                bytes_written = 0
                last_report_time = time.time()
                report_interval = 0.5  # 進捗報告の間隔 (秒)
                
                while True:
                    buffer = iso_file.read(buffer_size)
                    if not buffer:
                        break
                        
                    device.write(buffer)
                    bytes_written += len(buffer)
                    
                    # 定期的に進捗を報告
                    current_time = time.time()
                    if progress_callback and (current_time - last_report_time) >= report_interval:
                        progress_percent = min(100, int(bytes_written * 100 / iso_size))
                        progress_callback(progress_percent, "writing")
                        last_report_time = current_time
                
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

# テスト実行用
if __name__ == "__main__":
    print("Available ISO files:")
    iso_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "isos")
    isos = get_iso_files(iso_dir)
    for iso in isos:
        print(f"{iso['name']} - {iso['size_formatted']}")
