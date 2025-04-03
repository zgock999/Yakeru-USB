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
    
    try:
        # 方法1: lsblkコマンドでブロックデバイス情報を取得
        result = subprocess.run(
            ["lsblk", "-J", "-o", "NAME,SIZE,MODEL,VENDOR,MOUNTPOINT,REMOVABLE,TYPE"],
            capture_output=True, text=True
        )
        
        if result.returncode == 0:
            data = json.loads(result.stdout)
            
            # リムーバブルディスクのみフィルタリング
            for device in data.get("blockdevices", []):
                if device.get("type") == "disk" and device.get("removable") == "1":
                    # 追加されたUSBデバイスの詳細情報を取得
                    devices.append({
                        "id": f"/dev/{device['name']}",
                        "name": device.get("model", "Unknown Device"),
                        "size": device.get("size", "Unknown"),
                        "vendor": device.get("vendor", "Unknown"),
                        "mountpoint": device.get("mountpoint")
                    })
        else:
            # 代替方法: ls -la /dev/sd* で直接デバイスを取得
            alt_result = subprocess.run(
                ["ls", "-la", "/dev/sd*"],
                capture_output=True, text=True
            )
            
            if alt_result.returncode == 0:
                lines = alt_result.stdout.strip().split('\n')
                for line in lines:
                    if "brw-" in line and "/dev/sd" in line:
                        # /dev/sda, /dev/sdb などのデバイス名を抽出
                        match = re.search(r'/dev/sd[a-z]+', line)
                        if match:
                            dev_path = match.group(0)
                            # パーティションを除外（/dev/sda1 などは除外）
                            if not re.search(r'/dev/sd[a-z]+\d+', dev_path):
                                # デバイス情報の取得
                                name = _get_device_model(dev_path) or "USB Storage"
                                size = _get_device_size(dev_path) or "Unknown"
                                devices.append({
                                    "id": dev_path,
                                    "name": name,
                                    "size": size,
                                    "vendor": "Unknown",
                                    "mountpoint": _get_device_mountpoint(dev_path)
                                })
            
            # さらに代替方法: /sys/block/sd* ディレクトリを確認
            if not devices:
                for dev_name in os.listdir("/sys/block"):
                    if dev_name.startswith("sd"):
                        removable_path = f"/sys/block/{dev_name}/removable"
                        if os.path.exists(removable_path):
                            with open(removable_path, 'r') as f:
                                if f.read().strip() == "1":
                                    dev_path = f"/dev/{dev_name}"
                                    name = _get_device_model(dev_path) or "USB Storage"
                                    size = _get_device_size(dev_path) or "Unknown"
                                    devices.append({
                                        "id": dev_path,
                                        "name": name,
                                        "size": size,
                                        "vendor": "Unknown",
                                        "mountpoint": _get_device_mountpoint(dev_path)
                                    })
                
        # USBデバイスが検出されなかった場合、udevadmコマンドも試す
        if not devices:
            devices = _detect_usb_devices_with_udevadm()
                
        return devices
    except Exception as e:
        print(f"Error listing Linux USB devices: {e}")
        # エラーが発生した場合、代替の検出方法を試行
        try:
            return _detect_usb_devices_with_udevadm()
        except Exception as e2:
            print(f"Failed to detect USB devices with alternative method: {e2}")
            return []

def _detect_usb_devices_with_udevadm():
    """udevadmコマンドを使用してUSBデバイスを検出"""
    devices = []
    try:
        # すべてのブロックデバイスを取得
        result = subprocess.run(
            ["udevadm", "info", "--query=property", "--name=/dev/sda"],
            capture_output=True, text=True
        )
        
        # USBバスに接続されているデバイスのみをフィルタリング
        if "ID_BUS=usb" in result.stdout:
            dev_path = "/dev/sda"
            name = _get_device_model(dev_path) or "USB Storage"
            size = _get_device_size(dev_path) or "Unknown"
            devices.append({
                "id": dev_path,
                "name": name,
                "size": size,
                "vendor": "Unknown",
                "mountpoint": _get_device_mountpoint(dev_path)
            })
            
        # sdbからsdzまでチェック
        for c in 'bcdefghijklmnopqrstuvwxyz':
            dev_path = f"/dev/sd{c}"
            if os.path.exists(dev_path):
                try:
                    result = subprocess.run(
                        ["udevadm", "info", "--query=property", f"--name={dev_path}"],
                        capture_output=True, text=True
                    )
                    if "ID_BUS=usb" in result.stdout:
                        name = _get_device_model(dev_path) or "USB Storage"
                        size = _get_device_size(dev_path) or "Unknown"
                        devices.append({
                            "id": dev_path,
                            "name": name,
                            "size": size,
                            "vendor": "Unknown",
                            "mountpoint": _get_device_mountpoint(dev_path)
                        })
                except:
                    pass
        
        return devices
    except Exception as e:
        print(f"Error detecting USB devices with udevadm: {e}")
        return []

def _get_device_model(dev_path):
    """デバイスのモデル名を取得"""
    try:
        # デバイスのモデル名を取得
        result = subprocess.run(
            ["lsblk", "-no", "MODEL", dev_path],
            capture_output=True, text=True
        )
        # 複数行の場合は最初の行だけを返す
        model_output = result.stdout.strip().split('\n')[0]
        return model_output or "USB Storage"
    except:
        return "USB Storage"

def _get_device_size(dev_path):
    """デバイスのサイズを取得"""
    try:
        # デバイスのサイズを取得
        result = subprocess.run(
            ["lsblk", "-no", "SIZE", dev_path],
            capture_output=True, text=True
        )
        # 複数行の場合は最初の行だけを返す
        size_output = result.stdout.strip().split('\n')[0]
        return size_output or "Unknown"
    except:
        return "Unknown"

def _get_device_mountpoint(dev_path):
    """デバイスのマウントポイントを取得"""
    try:
        # デバイスのマウントポイントを取得
        result = subprocess.run(
            ["lsblk", "-no", "MOUNTPOINT", dev_path],
            capture_output=True, text=True
        )
        # 複数行の場合は最初の行だけを返す
        mountpoint_output = result.stdout.strip().split('\n')[0]
        return mountpoint_output or None
    except:
        return None

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
