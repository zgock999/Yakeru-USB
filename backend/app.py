from flask import Flask, jsonify, request
from flask_cors import CORS
import os
import json
from flask_socketio import SocketIO
from usb_detector import list_usb_devices
from iso_writer import write_iso_to_device, get_iso_files

app = Flask(__name__)
CORS(app)
socketio = SocketIO(app, cors_allowed_origins="*")

ISO_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "isos")

# 書き込み状態追跡用のグローバル変数
write_status = {
    "progress": 0,
    "status": "idle"
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

def progress_callback(progress, status):
    """書き込み進捗をWebSocketで通知し、ステータスを更新"""
    global write_status
    write_status = {"progress": progress, "status": status}
    socketio.emit('write_progress', {'progress': progress, 'status': status})

if __name__ == '__main__':
    # フォルダが存在しない場合は作成
    os.makedirs(ISO_DIR, exist_ok=True)
    socketio.run(app, debug=True, host='0.0.0.0', port=5000)
