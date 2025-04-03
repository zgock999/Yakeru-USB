import os
import re
import platform
import subprocess
import json
import ctypes

def list_usb_devices():
    """システム上のUSBブロックデバイスを検出して返す"""
    system = platform.system()
    
    if system == "Linux":
        return _list_linux_usb_devices()
    elif system == "Windows":
        return _list_windows_usb_devices()
    elif system == "Darwin":  # macOS
        return _list_macos_usb_devices()
    else:
        raise NotImplementedError(f"Unsupported operating system: {system}")

def _list_linux_usb_devices():
    """Linuxシステム上のUSBブロックデバイスを検出"""
    devices = []
    
    # lsblkコマンドでブロックデバイス情報を取得
    try:
        result = subprocess.run(
            ["lsblk", "-J", "-o", "NAME,SIZE,MODEL,VENDOR,MOUNTPOINT,REMOVABLE,TYPE"],
            capture_output=True, text=True, check=True
        )
        
        data = json.loads(result.stdout)
        
        # リムーバブルディスクのみフィルタリング
        for device in data.get("blockdevices", []):
            if device.get("type") == "disk" and device.get("removable") == "1":
                devices.append({
                    "id": f"/dev/{device['name']}",
                    "name": device.get("model", "Unknown Device"),
                    "size": device.get("size", "Unknown"),
                    "vendor": device.get("vendor", "Unknown"),
                    "mountpoint": device.get("mountpoint")
                })
                
        return devices
    except Exception as e:
        print(f"Error listing Linux USB devices: {e}")
        return []

def _list_windows_usb_devices():
    """Windowsシステム上のUSBブロックデバイスを検出"""
    devices = []
    
    try:
        # WMIクエリを使用してリムーバブルディスクを取得
        result = subprocess.run(
            ["powershell", "-Command", 
             "Get-Disk | Where-Object { $_.BusType -eq 'USB' } | " +
             "Select-Object Number, FriendlyName, Size, OperationalStatus | " +
             "ConvertTo-Json"],
            capture_output=True, text=True, check=True
        )
        
        if result.stdout.strip():
            data = json.loads(result.stdout)
            # 単一のディスクの場合、リストに変換
            if not isinstance(data, list):
                data = [data]
                
            for disk in data:
                disk_number = disk.get('Number')
                # 管理者権限と適切なディスク形式を確認
                if _is_disk_accessible(disk_number):
                    devices.append({
                        "id": f"\\\\?\\PhysicalDrive{disk_number}",  # 修正: 適切なデバイスパス形式
                        "name": disk.get("FriendlyName", "Unknown Device"),
                        "size": f"{disk.get('Size', 0) // (1024**3)} GB",
                        "status": disk.get("OperationalStatus", "Unknown")
                    })
                
        return devices
    except Exception as e:
        print(f"Error listing Windows USB devices: {e}")
        return []

def _is_disk_accessible(disk_number):
    """Windowsでディスクがアクセス可能かどうかをチェック"""
    try:
        # Windowsのhandle関数でディスクを開いてテスト
        device_path = f"\\\\.\\PhysicalDrive{disk_number}"
        
        # 管理者権限がなければFalseを返す
        if not _is_admin():
            print(f"Warning: Admin privileges required to access {device_path}")
            return False
            
        # FILEオブジェクトをCtypesで開いてみる
        GENERIC_READ = 0x80000000
        OPEN_EXISTING = 3
        FILE_SHARE_READ = 0x00000001
        FILE_SHARE_WRITE = 0x00000002
        
        handle = ctypes.windll.kernel32.CreateFileW(
            device_path,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            None,
            OPEN_EXISTING,
            0,
            None
        )
        
        if handle == -1:  # INVALID_HANDLE_VALUE
            return False
        
        # ハンドルを閉じる
        ctypes.windll.kernel32.CloseHandle(handle)
        return True
    except Exception as e:
        print(f"Error checking disk accessibility: {e}")
        return False

def _is_admin():
    """現在のプロセスが管理者権限で実行されているかチェック"""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except:
        return False

def _list_macos_usb_devices():
    """macOSシステム上のUSBブロックデバイスを検出"""
    devices = []
    
    try:
        # diskutilコマンドでディスク情報を取得
        result = subprocess.run(
            ["diskutil", "list", "-plist", "external"],
            capture_output=True, text=True, check=True
        )
        
        import plistlib
        data = plistlib.loads(result.stdout.encode())
        
        for disk_name in data.get("AllDisksAndPartitions", []):
            device_id = f"/dev/{disk_name.get('DeviceIdentifier')}"
            
            # 追加情報の取得
            info_result = subprocess.run(
                ["diskutil", "info", "-plist", device_id],
                capture_output=True, text=True, check=True
            )
            info_data = plistlib.loads(info_result.stdout.encode())
            
            devices.append({
                "id": device_id,
                "name": info_data.get("MediaName", "Unknown Device"),
                "size": info_data.get("TotalSize", 0) // (1024**2),
                "removable": info_data.get("Removable", True),
                "ejectable": info_data.get("Ejectable", True)
            })
            
        return devices
    except Exception as e:
        print(f"Error listing macOS USB devices: {e}")
        return []

if __name__ == "__main__":
    # テスト実行
    devices = list_usb_devices()
    print(json.dumps(devices, indent=2))
