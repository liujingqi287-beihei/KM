@echo off
setlocal

::修改这里为你要清空的文件夹
set "TargetPath=D:\DataCache"

echo 即将删除文件夹下的所有文件:
echo "%TargetPath%"
echo 此操作不可恢复
echo.
set /p confirm=请输入 YES 确认执行,其他任意键取消:
if /i not "%confirm%"=="YES" (
	echo 操作取消
	pause
	exit /b
)

echo.
echo 正在删除文件，请稍等...

for /f "delims=" %%i in ('dir /b /a-d "%TargetPath%\*" 2^>nul') do (
	echo 删除文件: %%i
	del /q /f "%TargetPath%\%%i" 2>nul
)
echo.
echo 文件删除完成！
pause