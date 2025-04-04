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
# CORS設定を修正して認証関連のヘッダーを許可
CORS(app, supports_credentials=True, expose_headers=['Authorization'])
socketio = SocketIO(app, cors_allowed_origins="*")

ISO_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "isos")

# 書き込み状態追跡用のグローバル変数
write_status = {
    "progress": 0,
    "status": "idle"
}

# 書き込み中フラグを追加
is_writing_active = False

# デバッグ用のミドルウェアを追加して、すべてのリクエストとレスポンスをログ出力
@app.before_request
def log_request_info():
    """リクエスト情報をログに記録"""
    app.logger.debug('Request Headers: %s', request.headers)
    app.logger.debug('Request Body: %s', request.get_data())

@app.after_request
def log_response_info(response):
    """レスポンス情報をログに記録"""
    app.logger.debug('Response Status: %s', response.status)
    app.logger.debug('Response Headers: %s', response.headers)
    return response

# アプリケーション起動時に書き込み状態をリセットする
@app.before_first_request
def reset_write_status_on_startup():
    """サーバー起動時に書き込み状態をリセット"""
    global write_status, is_writing_active
    write_status = {
        "progress": 0,
        "status": "idle"
    }
    is_writing_active = False
    print("Write status reset on startup")

# ヘルスチェック用のエンドポイント追加
@app.route('/api/health', methods=['GET'])
def health_check():
    """APIサーバーの状態確認用エンドポイント"""
    return jsonify({
        "status": "ok", 
        "version": "1.0",
        "timestamp": time.time()
    })

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
    global is_writing_active
    
    try:
        # 書き込み中はデバイス一覧取得をブロック
        if is_writing_active:
            print("Blocked USB device scan during active write operation")
            return jsonify({"devices": [], "blocked": True, "message": "Device scan blocked during write operation"}), 423
            
        devices = list_usb_devices()
        return jsonify({"devices": devices})
    except Exception as e:
        print(f"Error in get_usb_devices: {str(e)}")
        return jsonify({"error": str(e), "devices": []}), 500  # 500を返してクライアント側でもエラー処理
        
@app.route('/api/write', methods=['POST'])
def write_iso():
    """ISOファイルをUSBデバイスに書き込み開始"""
    global write_status, is_writing_active
    
    try:
        # 書き込み中なら新たな書き込みを拒否
        if is_writing_active:
            return jsonify({"error": "Write operation already in progress"}), 409
            
        data = request.json
        iso_file = data.get('iso_file')
        device = data.get('device')
        
        if not iso_file or not device:
            return jsonify({"error": "ISO file and device must be specified"}), 400
            
        iso_path = os.path.join(ISO_DIR, iso_file)
        
        if not os.path.exists(iso_path):
            return jsonify({"error": f"ISO file {iso_file} not found"}), 404
        
        # 書き込み状態をリセットし、書き込み中フラグを設定
        write_status = {"progress": 0, "status": "started"}
        is_writing_active = True
            
        # 非同期で書き込み処理を開始
        socketio.start_background_task(
            write_iso_to_device_wrapper, iso_path, device, progress_callback
        )
        
        return jsonify({"status": "Writing started"})
    except Exception as e:
        # エラー時は書き込み中フラグを解除
        is_writing_active = False
        return jsonify({"error": str(e)}), 500

# 書き込み処理のラッパー関数を追加（書き込み完了時にフラグをリセットする）
def write_iso_to_device_wrapper(iso_path, device_path, callback):
    global is_writing_active
    
    try:
        result = write_iso_to_device(iso_path, device_path, callback)
        return result
    except Exception as e:
        # 例外をそのまま伝搬
        raise
    finally:
        # 書き込み完了後に処理が行われるが、
        # is_writing_activeフラグは既にcallback内でリセットされているため
        # ここでは追加のリセット処理を行わない（二重リセット防止）
        print("Write operation completed in wrapper function")

@app.route('/api/write-status', methods=['GET'])
def get_write_status():
    """現在の書き込み状態を取得するエンドポイント（ポーリング用）"""
    global write_status
    
    # 状態オブジェクトをそのまま返す
    # すでにここでは状態を上書きしないため、常に最新のステータスが返される
    # (completedのステータスはリセットリクエストが来るまで維持される)
    
    return jsonify(write_status)

@app.route('/api/rescan-usb', methods=['POST'])
def rescan_usb():
    """USBデバイスを再スキャンする（Linuxのudevtrigger用）"""
    global is_writing_active
    
    try:
        # 書き込み中はデバイス再スキャンをブロック
        if is_writing_active:
            print("Blocked USB device rescan during active write operation")
            return jsonify({"devices": [], "blocked": True, "message": "Rescan blocked during write operation"}), 423
            
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
                
                # デバイスファイルの権限を確認/更新（エラーを無視）
                try:
                    # sdドライブの場合
                    if os.path.exists("/dev/sd*"):
                        subprocess.run(
                            ["chmod", "-R", "a+rw", "/dev/sd*"],
                            check=False
                        )
                    # vdドライブの場合（仮想環境向け）
                    if os.path.exists("/dev/vd*"):
                        subprocess.run(
                            ["chmod", "-R", "a+rw", "/dev/vd*"],
                            check=False
                        )
                    # nvmeドライブの場合
                    if os.path.exists("/dev/nvme*"):
                        subprocess.run(
                            ["chmod", "-R", "a+rw", "/dev/nvme*"],
                            check=False
                        )
                except Exception as e:
                    print(f"Warning: Permission setting failed: {e}")
            except Exception as e:
                print(f"Warning: Failed to trigger USB rescan: {e}")
        
        # 更新されたデバイス一覧を返す
        devices = list_usb_devices()
        return jsonify({"devices": devices})
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/reset-status', methods=['POST'])
def reset_status():
    """書き込み状態を明示的にリセットするエンドポイント"""
    global write_status, is_writing_active
    
    # フロントエンドからのリクエストでのみステータスをリセット
    write_status = {
        "progress": 0,
        "status": "idle"
    }
    
    # 書き込み中フラグも強制的にリセット
    is_writing_active = False
    
    print("Status explicitly reset by frontend request")
    
    return jsonify({"status": "Status reset successfully"})

# 進捗コールバック関数を修正
def progress_callback(progress, status):
    """書き込み進捗をWebSocketで通知"""
    global write_status, is_writing_active
    
    try:
        # 基本的な進捗情報を更新
        write_status["progress"] = progress
        write_status["status"] = status
        
        # エラーステータスの場合はログに出力
        if status and status.startswith("error"):
            print(f"Error occurred during writing: {status}")
            # エラー通知が確実に送信された後でフラグをリセットするため、ここではリセットしない
        
        # 完了通知の場合はログに出力
        if status == "completed":
            print(f"Write process completed with status: {status}, progress: {progress}%")
            # 完了状態を維持するため、is_writing_activeのみをリセットし、ステータスはcompletedのまま
        
        # ソケットで通知
        progress_data = {
            'progress': progress, 
            'status': status
        }
        
        # 完了通知とエラー通知は確実に送信
        if status == "completed" or (status and status.startswith("error")):
            # 確実に通知されるように、より頻繁に送信
            for i in range(8):  # 8回に増やす
                print(f"Sending {status} notification (attempt {i+1}/8)")
                socketio.emit('write_progress', progress_data)
                # REST APIでのポーリングにも対応するためwrite_statusを確実に更新
                write_status["progress"] = progress
                write_status["status"] = status
                # 待機時間を調整
                time.sleep(0.2)
            
            # 全ての通知が送信された後にフラグをリセット(書き込み中フラグのみリセット、ステータスはそのまま)
            if status == "completed" or status.startswith("error"):
                print(f"All notifications sent for {status}, now resetting writing active flag only")
                is_writing_active = False  # 重要: 書き込み中フラグのみリセット
                # write_statusのステータスは変更しない
        else:
            socketio.emit('write_progress', progress_data)
    except Exception as e:
        # 例外発生時は書き込み中フラグをリセット
        is_writing_active = False
        print(f"Error in progress_callback: {e}")
        # 通知失敗時でもステータスは更新
        write_status["status"] = f"error: {str(e)}"

@app.errorhandler(Exception)
def handle_exception(e):
    """すべての未処理例外をキャッチするグローバルエラーハンドラ"""
    print(f"Unhandled exception: {str(e)}")
    return jsonify({
        "error": "Internal server error",
        "message": str(e)
    }), 500

if __name__ == '__main__':
    # フォルダが存在しない場合は作成
    os.makedirs(ISO_DIR, exist_ok=True)
    socketio.run(app, debug=True, host='0.0.0.0', port=5000)
