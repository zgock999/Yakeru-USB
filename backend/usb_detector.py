import os
import re
import platform
import subprocess
import json

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
                devices.append({
                    "id": f"\\\\.\\PhysicalDrive{disk['Number']}",
                    "name": disk.get("FriendlyName", "Unknown Device"),
                    "size": f"{disk.get('Size', 0) // (1024**3)} GB",
                    "status": disk.get("OperationalStatus", "Unknown")
                })
                
        return devices
    except Exception as e:
        print(f"Error listing Windows USB devices: {e}")
        return []

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
