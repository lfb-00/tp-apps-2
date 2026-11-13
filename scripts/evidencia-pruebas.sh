#!/usr/bin/env bash
# Corre las pruebas automatizadas y deja su resultado en docs/evidencias/08-pruebas.txt.
#
#   bash scripts/evidencia-pruebas.sh
#
# El informe (seccion 5.4) enlaza ese archivo en vez de copiar el total de pruebas: asi agregar
# una prueba no obliga a editar la documentacion, y el numero que se entrega sale de una corrida
# real y no de alguien que lo escribio a mano. Lo llama scripts/evidencias.sh; se puede correr
# solo al cerrar una entrega. Solo necesita el SDK de .NET 10 (las pruebas usan InMemory).
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SALIDA="$RAIZ/docs/evidencias/08-pruebas.txt"
cd "$RAIZ"

echo "==> Compilando desde cero y corriendo las pruebas"
TEMPORAL="$(mktemp)"
trap 'rm -f "$TEMPORAL"' EXIT

# Compilacion completa: una incremental no repite las advertencias y el conteo saldria en cero.
codigo=0
{ dotnet build RepMatch.slnx --nologo --no-incremental && dotnet test RepMatch.slnx --nologo --no-build; } 2>&1 \
  | tee "$TEMPORAL" || codigo=$?

# Advertencias de compilacion (el proyecto se compromete a cero), del resumen de dotnet build en
# espaniol o en ingles: "   0 Advertencia(s)" / "   0 Warning(s)".
advertencias="$(grep -oE '^ *[0-9]+ (Advertencia\(s\)|Warning\(s\))' "$TEMPORAL" | tail -1 | grep -oE '[0-9]+' || echo '?')"

{
  echo "# Pruebas automatizadas de RepMatch"
  echo "# Generado por scripts/evidencia-pruebas.sh. No editar a mano."
  echo
  echo "Fecha:        $(date '+%Y-%m-%d %H:%M %z')"
  echo "Commit:       $(git rev-parse --short HEAD 2>/dev/null || echo 'sin git')$(git diff --quiet HEAD -- . ':!docs/evidencias/08-pruebas.txt' 2>/dev/null || echo ' + cambios sin commitear')"
  echo "SDK:          $(dotnet --version)"
  echo "Comando:      dotnet build RepMatch.slnx --no-incremental && dotnet test RepMatch.slnx --no-build"
  echo "Advertencias: $advertencias"
  echo "Resultado:    $([ "$codigo" -eq 0 ] && echo 'todas correctas' || echo "con errores (codigo $codigo)")"
  echo
  echo "## Resumen por proyecto de pruebas"
  echo
  # La linea de resumen de dotnet test, en espaniol o en ingles segun el idioma del SDK.
  grep -E '(Correctas!|Con error!|Passed!|Failed!) +- ' "$TEMPORAL" | sed 's/^ *//' || echo "(no se encontro la linea de resumen)"
} > "$SALIDA"

echo
echo "Evidencia escrita en docs/evidencias/08-pruebas.txt"
cat "$SALIDA"
exit "$codigo"
