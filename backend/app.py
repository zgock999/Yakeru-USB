from flask import Flask, jsonify, request
from flask_cors import CORS
import os
import json
from flask_socketio import SocketIO
from usb_detector import list_usb_devices
from iso_writer import write_iso_to_device, get_iso_files
import platform
import subprocess
import time

app = Flask(__name__)
CORS(app)
socketio = SocketIO(app, cors_allowed_origins="*")

ISO_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "isos")

# 書き込み状態追跡用のグローバル変数
write_status = {
    "progress": 0,
    "status": "idle",
    "start_time": None,
    "estimated_time": None,
    "total_size": 0,
    "bytes_written": 0
}

@app.route('/api/isos', methods=['GET'])
def get_isos():
    """ISOファイルの一覧を取得"""
    try:
        iso_files = get_iso_files(ISO_DIR)
        return jsonify({"isos": iso_files})
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/usb-devices', methods=['GET'])
def get_usb_devices():
    """利用可能なUSBデバイスの一覧を取得"""
    try:
        devices = list_usb_devices()
        return jsonify({"devices": devices})
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/write', methods=['POST'])
def write_iso():
    """ISOファイルをUSBデバイスに書き込み開始"""
    try:
        data = request.json
        iso_file = data.get('iso_file')
        device = data.get('device')
        
        if not iso_file or not device:
            return jsonify({"error": "ISO file and device must be specified"}), 400
            
        iso_path = os.path.join(ISO_DIR, iso_file)
        
        if not os.path.exists(iso_path):
            return jsonify({"error": f"ISO file {iso_file} not found"}), 404
        
        # 書き込み状態をリセット
        global write_status
        write_status = {"progress": 0, "status": "started"}
            
        # 非同期で書き込み処理を開始
        socketio.start_background_task(
            write_iso_to_device, iso_path, device, progress_callback
        )
        
        return jsonify({"status": "Writing started"})
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/write-status', methods=['GET'])
def get_write_status():
    """現在の書き込み状態を取得するエンドポイント（ポーリング用）"""
    global write_status
    return jsonify(write_status)

@app.route('/api/rescan-usb', methods=['POST'])
def rescan_usb():
    """USBデバイスを再スキャンする（Linuxのudevtrigger用）"""
    try:
        if platform.system() == "Linux":
            try:
                # udevadmコマンドでUSBデバイスを再検出
                subprocess.run(
                    ["udevadm", "trigger"],
                    check=True
                )
                subprocess.run(
                    ["udevadm", "settle"],
                    check=True
                )
                # デバイスファイルの権限を確認/更新
                subprocess.run(
                    ["chmod", "-R", "a+rw", "/dev/sd*"],
                    check=False
                )
            except Exception as e:
                print(f"Warning: Failed to trigger USB rescan: {e}")
        
        # 更新されたデバイス一覧を返す
        devices = list_usb_devices()
        return jsonify({"devices": devices})
    except Exception as e:
        return jsonify({"error": str(e)}), 500

def progress_callback(progress, status):
    """書き込み進捗をWebSocketで通知し、ステータスを更新"""
    global write_status
    
    # 状態に基づいて追加情報を設定
    if status == "started":
        write_status["start_time"] = time.time()
        write_status["estimated_time"] = None
    elif status == "writing" and write_status["start_time"] is not None:
        elapsed_time = time.time() - write_status["start_time"]
        if progress > 0:
            # 残りの推定時間を計算
            write_status["estimated_time"] = (elapsed_time / progress) * (100 - progress)
    
    # 基本的な進捗情報を更新
    write_status["progress"] = progress
    write_status["status"] = status
    
    # ソケットで通知
    progress_data = {
        'progress': progress, 
        'status': status
    }
    
    # 追加情報があれば含める
    if write_status["estimated_time"] is not None:
        progress_data["eta"] = int(write_status["estimated_time"])
        
    socketio.emit('write_progress', progress_data)

if __name__ == '__main__':
    # フォルダが存在しない場合は作成
    os.makedirs(ISO_DIR, exist_ok=True)
    socketio.run(app, debug=True, host='0.0.0.0', port=5000)
