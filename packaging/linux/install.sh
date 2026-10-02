#!/usr/bin/env bash
#
# Integrazione desktop Linux per AppConfig Editor (per l'utente corrente).
#
# Installa le icone nel tema hicolor e la voce di menu (.desktop) in
# $XDG_DATA_HOME (~/.local/share). Se viene passato un eseguibile come primo
# argomento, viene copiato in ~/.local/bin e usato come comando della voce.
#
# Uso:
#   ./install.sh                                   # assume "AppConfigEditor.App" nel PATH
#   ./install.sh /percorso/AppConfigEditor.App     # installa anche il binario
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
app_id="appconfigeditor"
desktop_id="AppConfigEditor"
binary_name="AppConfigEditor.App"

# 1) Icone nel tema hicolor (piu' dimensioni).
for png in "$script_dir"/icons/hicolor/*/apps/"$app_id".png; do
    [ -e "$png" ] || continue
    theme_dir="$(basename "$(dirname "$(dirname "$png")")")"   # es. 512x512
    install -Dm644 "$png" "$data_home/icons/hicolor/$theme_dir/apps/$app_id.png"
done

# 2) Binario (opzionale).
exec_line="$binary_name"
if [ "$#" -ge 1 ]; then
    src_bin="$1"
    if [ ! -f "$src_bin" ]; then
        echo "Eseguibile non trovato: $src_bin" >&2
        exit 1
    fi
    install -Dm755 "$src_bin" "$HOME/.local/bin/$binary_name"
    exec_line="$HOME/.local/bin/$binary_name"
fi

# 3) Voce di menu, con Exec puntato al binario scelto.
desktop_dest="$data_home/applications/$desktop_id.desktop"
install -Dm644 "$script_dir/$desktop_id.desktop" "$desktop_dest"
sed -i "s|^Exec=.*|Exec=$exec_line|" "$desktop_dest"

# 4) Aggiorna le cache del desktop, se gli strumenti sono disponibili.
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data_home/applications" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$data_home/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Installato: $desktop_dest"
echo "Icone:      $data_home/icons/hicolor/*/apps/$app_id.png"
if [ "$exec_line" != "$binary_name" ]; then
    echo "Binario:    $exec_line"
fi
