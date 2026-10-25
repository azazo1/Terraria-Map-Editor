#!/bin/bash
# 把 dotnet publish 的输出打包成可分发的 .app 与 .dmg.
# 用法: make-macos-bundle.sh <publish 目录> <输出目录>
set -euo pipefail

SRC="${1:?需要 publish 目录}"
OUT="${2:?需要输出目录}"

APP="$OUT/TEdit5.app"
DMG="$OUT/TEdit5-osx-arm64.dmg"

rm -rf "$APP" "$DMG"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp "$SRC/TEdit5" "$APP/Contents/MacOS/"
for lib in libSkiaSharp.dylib libAvaloniaNative.dylib libHarfBuzzSharp.dylib; do
    if [ -f "$SRC/$lib" ]; then
        cp "$SRC/$lib" "$APP/Contents/MacOS/"
    fi
done
chmod +x "$APP/Contents/MacOS/TEdit5"

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>TEdit5</string>
    <key>CFBundleDisplayName</key><string>TEdit5</string>
    <key>CFBundleIdentifier</key><string>org.tedit.tedit5</string>
    <key>CFBundleVersion</key><string>1.0</string>
    <key>CFBundleShortVersionString</key><string>1.0</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleExecutable</key><string>TEdit5</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSMinimumSystemVersion</key><string>12.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

# 临时签名: 没有开发者证书时也能在本机运行, 分发时用户需右键打开.
codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || echo "ad-hoc 签名失败, 继续打包"

# dmg 内附 Applications 快捷方式, 拖拽即装.
STAGE="$(mktemp -d)"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "TEdit5" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
rm -rf "$STAGE"

echo "产物: $APP"
echo "产物: $DMG"
ls -lh "$DMG" | awk '{print $5, $9}'
