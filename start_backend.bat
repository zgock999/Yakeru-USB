@echo off
echo Yakeru-USB バックエンドサーバーを起動します...
echo 管理者権限が必要です。
echo.

REM 管理者権限で実行するためのVBSスクリプトを作成
echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
set params= %*
echo UAC.ShellExecute "cmd.exe", "/c ""%~s0"" %params% elevate", "", "runas", 1 >> "%temp%\getadmin.vbs"

REM 管理者権限がない場合は昇格させる
if '%1'=='elevate' goto start
"%temp%\getadmin.vbs"
del "%temp%\getadmin.vbs"
exit /B

:start
echo USBデバイスを検出して、ISOファイルを書き込みます。
cd /d "%~dp0backend"

REM 仮想環境があれば有効化
if exist venv\Scripts\activate.bat (
    echo 仮想環境を有効化しています...
    call venv\Scripts\activate.bat
)

REM アプリケーションを起動
echo バックエンドサーバーを起動しています...
python app.py

pause
