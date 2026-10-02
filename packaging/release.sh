#!/usr/bin/env bash
#
# Build di release di AppConfig Editor per Linux e Windows.
#
# Cosa fa:
#   1) build Release della solution (deve essere pulita, 0 warning/errori);
#   2) test del progetto Core;
#   3) publish self-contained single-file per linux-x64 e win-x64 senza
#      simboli di debug (DebugType=none; eventuali .pdb residui rimossi);
#   4) archivi in dist/ con i file nella radice: AppConfigEditor-<rid>.zip
#      (l'eseguibile Linux conserva il bit di esecuzione).
#
# Uso:
#   packaging/release.sh
#
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solution="$root/AppConfigEditor.slnx"
project="$root/src/AppConfigEditor.App/AppConfigEditor.App.csproj"
dist="$root/dist"
config="Release"
tfm="net10.0"
rids=(linux-x64 win-x64)

info()  { printf '\n\033[1;35m==> %s\033[0m\n' "$*"; }
error() { printf 'ERRORE: %s\n' "$*" >&2; }

# SDK .NET 10: preferisce quella utente (~/.dotnet) solo se è una 10.x,
# altrimenti usa quella nel PATH (il global.json vincola comunque la 10).
if [ -x "$HOME/.dotnet/dotnet" ]; then
    case "$("$HOME/.dotnet/dotnet" --version 2>/dev/null || true)" in
        10.*) export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH" ;;
    esac
fi

if ! command -v dotnet >/dev/null 2>&1; then
    error "dotnet non trovato nel PATH. Esporta la SDK 10, ad esempio:"
    error '  export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"'
    exit 1
fi
info "SDK: $(dotnet --version)"

# Crea "<out>.zip" con il contenuto di <src> nella radice, preservando i
# permessi (bit di esecuzione su Linux). Usa python3, con fallback su zip.
pack_zip() {
    local src="$1" out="$2"
    rm -f "$out"
    if command -v python3 >/dev/null 2>&1; then
        python3 - "$src" "$out" <<'PY'
import os
import sys
import zipfile

src, out = sys.argv[1], sys.argv[2]
names = sorted(
    name for name in os.listdir(src) if os.path.isfile(os.path.join(src, name))
)
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name in names:
        path = os.path.join(src, name)
        entry = zipfile.ZipInfo(name)
        entry.compress_type = zipfile.ZIP_DEFLATED
        entry.external_attr = (os.stat(path).st_mode & 0xFFFF) << 16
        with open(path, "rb") as handle:
            archive.writestr(entry, handle.read())
print("  %s (%s)" % (out, ", ".join(names)))
PY
    elif command -v zip >/dev/null 2>&1; then
        (cd "$src" && zip -q -r -X "$out" .)
        echo "  $out"
    else
        error "serve python3 oppure zip per creare gli archivi"
        return 1
    fi
}

# 1) Build + test.
info "Build ($config)"
dotnet build "$solution" -c "$config"

info "Test"
dotnet test "$solution" -c "$config"

# 2) Publish per ogni RID e crea l'archivio.
mkdir -p "$dist"
for rid in "${rids[@]}"; do
    publish_dir="$root/src/AppConfigEditor.App/bin/$config/$tfm/$rid/publish"
    info "Publish $rid"
    rm -rf "$publish_dir"
    dotnet publish "$project" -c "$config" -r "$rid" --self-contained \
        -p:PublishSingleFile=true -p:DebugType=none
    find "$publish_dir" -name '*.pdb' -delete
    pack_zip "$publish_dir" "$dist/AppConfigEditor-$rid.zip"
done

info "Artefatti in $dist"
ls -lh "$dist"