#!/usr/bin/env bash
# Builds PayBeat.app (a menu-bar style app without a Dock icon) and zips it.
# Usage: scripts/build-mac-app.sh <rid: osx-arm64|osx-x64> <version> <output-zip>
set -euo pipefail

rid="${1:?runtime identifier required (osx-arm64 or osx-x64)}"
version="${2:?version required, e.g. 1.2.3}"
zip_path="${3:?output zip path required}"

root="$(cd "$(dirname "$0")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

app="$work/PayBeat.app"
macos="$app/Contents/MacOS"
resources="$app/Contents/Resources"
mkdir -p "$macos" "$resources"

dotnet publish "$root/src/PayBeat.App/PayBeat.App.csproj" -c Release -r "$rid" --self-contained -o "$macos"

# Build the .icns from the 256px PNG.
iconset="$work/PayBeat.iconset"
mkdir -p "$iconset"
png="$root/src/PayBeat.App/Resources/Icons/PayBeat.png"
for size in 16 32 128 256; do
  sips -z "$size" "$size" "$png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
done
for size in 16 128; do
  double=$((size * 2))
  sips -z "$double" "$double" "$png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$resources/PayBeat.icns"

cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>PayBeat</string>
  <key>CFBundleDisplayName</key><string>PayBeat</string>
  <key>CFBundleExecutable</key><string>PayBeat</string>
  <key>CFBundleIdentifier</key><string>com.paybeat.app</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>PayBeat</string>
  <key>CFBundleShortVersionString</key><string>${version}</string>
  <key>CFBundleVersion</key><string>${version}</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSUIElement</key><true/>
</dict>
</plist>
EOF

chmod +x "$macos/PayBeat"
# Ad-hoc signature: required for arm64 binaries to run; not a substitute for notarization.
codesign --force --deep --sign - "$app"

# ditto keeps permissions and the bundle structure intact (plain zip can break signatures).
ditto -c -k --keepParent "$app" "$zip_path"
