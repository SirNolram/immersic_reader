#!/usr/bin/env bash
# Prueba de punta a punta: API .NET + servicio de IA + Gemini simulado.
# Requiere haber compilado ambos proyectos:
#   (cd servicio-ia && npm install && npm run build)
#   dotnet build api/Immersic.Api.csproj -c Release
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMPORAL="$(mktemp -d)"
RESPUESTA="$TEMPORAL/respuesta.json"
API="http://127.0.0.1:5080"
PROCESOS=()

terminar() {
  for pid in "${PROCESOS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
}
trap terminar EXIT

node "$RAIZ/servicio-ia/dist/test/ejecutar-gemini-simulado.js" 4010 > "$TEMPORAL/gemini.log" 2>&1 &
PROCESOS+=($!)

PORT=3001 GEMINI_URL_BASE="http://127.0.0.1:4010/v1beta" \
  node "$RAIZ/servicio-ia/dist/src/index.js" > "$TEMPORAL/servicio-ia.log" 2>&1 &
PROCESOS+=($!)

(
  cd "$RAIZ/api"
  export ConnectionStrings__Immersic="Data Source=$TEMPORAL/prueba.db"
  export ServicioIa__UrlBase="http://127.0.0.1:3001/"
  export ASPNETCORE_URLS="$API"
  exec dotnet bin/Release/net8.0/Immersic.Api.dll
) > "$TEMPORAL/api.log" 2>&1 &
PROCESOS+=($!)

mostrar_registros() {
  for archivo in "$TEMPORAL"/*.log; do
    echo "--- $(basename "$archivo")"
    tail -n 40 "$archivo"
  done
}

esperar() {
  local url="$1" nombre="$2"
  for _ in $(seq 1 60); do
    if curl -fs "$url" > /dev/null 2>&1; then
      echo "listo  $nombre"
      return 0
    fi
    sleep 1
  done
  echo "No respondió: $nombre ($url)"
  mostrar_registros
  exit 1
}

# pedir METODO RUTA CODIGO_ESPERADO CUERPO [ENCABEZADO...]
pedir() {
  local metodo="$1" ruta="$2" esperado="$3" cuerpo="$4"
  shift 4
  local argumentos=(-s -o "$RESPUESTA" -w "%{http_code}" -X "$metodo" "$API$ruta")
  if [ -n "$cuerpo" ]; then
    argumentos+=(-H "Content-Type: application/json" --data-binary "$cuerpo")
  fi
  for encabezado in "$@"; do
    argumentos+=(-H "$encabezado")
  done

  local codigo
  codigo="$(curl "${argumentos[@]}")"
  if [ "$codigo" != "$esperado" ]; then
    echo "FALLÓ  $metodo $ruta -> $codigo (se esperaba $esperado)"
    cat "$RESPUESTA"
    echo
    mostrar_registros
    exit 1
  fi
  echo "ok     $metodo $ruta -> $codigo"
}

campo() {
  jq -r "$1" "$RESPUESTA"
}

comprobar() {
  local descripcion="$1" condicion="$2"
  if [ "$(jq -r "$condicion" "$RESPUESTA")" != "true" ]; then
    echo "FALLÓ  $descripcion"
    cat "$RESPUESTA"
    echo
    exit 1
  fi
  echo "ok     $descripcion"
}

esperar "http://127.0.0.1:3001/salud" "servicio de IA"
esperar "$API/api/salud" "API"

echo "== Salud"
pedir GET /api/salud 200 ""
comprobar "base de datos conectada" '.baseDeDatos == "conectada"'
comprobar "servicio de IA disponible" '.servicioIa == "disponible"'

echo "== Etiquetas"
pedir GET /api/etiquetas 200 ""
comprobar "12 etiquetas iniciales" 'length == 12'
CALMA="$(campo '.[] | select(.nombre == "calma") | .id')"
TERROR="$(campo '.[] | select(.nombre == "terror") | .id')"
MISTERIO="$(campo '.[] | select(.nombre == "misterio") | .id')"
FELIZ="$(campo '.[] | select(.nombre == "feliz") | .id')"
INTENSO="$(campo '.[] | select(.nombre == "intenso") | .id')"

pedir POST /api/etiquetas 201 '{"nombre":"Nostalgia","descripcion":"Recuerdos del pasado."}'
comprobar "el nombre se guarda en minúsculas" '.nombre == "nostalgia"'
NOSTALGIA="$(campo .id)"
pedir POST /api/etiquetas 409 '{"nombre":"NOSTALGIA"}'
pedir POST /api/etiquetas 400 '{"nombre":"dos palabras"}'
pedir PUT "/api/etiquetas/$NOSTALGIA" 200 '{"nombre":"nostalgia","descripcion":"Añoranza."}'
pedir DELETE "/api/etiquetas/$NOSTALGIA" 204 ""
pedir GET "/api/etiquetas/$NOSTALGIA" 404 ""

echo "== Libros"
pedir POST /api/libros 400 '{"titulo":""}'
pedir POST /api/libros 201 '{"titulo":"El faro de las mareas","autor":"Laura Gómez"}'
LIBRO="$(campo .id)"
pedir GET /api/libros 200 ""
comprobar "la lista tiene el libro" 'length == 1'
pedir GET "/api/libros?buscar=faro" 200 ""
comprobar "búsqueda por título" 'length == 1'
pedir PUT "/api/libros/$LIBRO" 200 '{"titulo":"El faro de las mareas (revisado)","autor":"Laura Gómez"}'
comprobar "título actualizado" '.titulo == "El faro de las mareas (revisado)"'
pedir GET /api/libros/999999 404 ""

echo "== Capítulos"
CONTENIDO="$(printf '%s\n\n%s\n\n%s\n\n%s' \
  'La mañana llegó despacio sobre el puerto y las gaviotas descansaban en los mástiles.' \
  'Esa noche algo golpeó la puerta del faro y Elena bajó con una vela temblando en la mano.' \
  '—¿Quién anda ahí? —preguntó, pero solo respondió el viento entre las rocas del acantilado.' \
  'Al amanecer todo el pueblo celebró en la plaza con música, risas y pan recién horneado.')"
pedir POST "/api/libros/$LIBRO/capitulos" 201 "$(jq -n --arg c "$CONTENIDO" '{titulo: "La tormenta", contenido: $c}')"
CAPITULO="$(campo .id)"
comprobar "número asignado automáticamente" '.numero == 1'
pedir POST "/api/libros/$LIBRO/capitulos" 409 '{"titulo":"Repetido","numero":1}'
pedir POST "/api/libros/$LIBRO/capitulos" 201 '{"titulo":"Epílogo"}'
EPILOGO="$(campo .id)"
comprobar "siguiente número disponible" '.numero == 2'
pedir GET "/api/libros/$LIBRO/capitulos" 200 ""
comprobar "el libro tiene dos capítulos" 'length == 2'
pedir GET "/api/capitulos/$CAPITULO" 200 ""
comprobar "el capítulo trae su contenido" '.caracteres > 300'

echo "== Pistas"
pedir POST /api/pistas 201 "{\"nombre\":\"Puerto en calma\",\"artista\":\"Orquesta Andina\",\"etiquetaIds\":[$CALMA]}"
PISTA_CALMA="$(campo .id)"
pedir POST /api/pistas 201 "{\"nombre\":\"Sombras del faro\",\"etiquetaIds\":[$TERROR,$INTENSO]}"
PISTA_TERROR="$(campo .id)"
pedir POST /api/pistas 201 "{\"nombre\":\"Fiesta en la plaza\",\"etiquetaIds\":[$FELIZ]}"
pedir POST /api/pistas 201 "{\"nombre\":\"Niebla\",\"etiquetaIds\":[$MISTERIO,$TERROR]}"
pedir POST /api/pistas 400 "{\"nombre\":\"Solo intensa\",\"etiquetaIds\":[$INTENSO]}"
pedir POST /api/pistas 400 '{"nombre":"Sin etiquetas","etiquetaIds":[]}'
pedir POST /api/pistas 400 "{\"nombre\":\"Cuatro\",\"etiquetaIds\":[$CALMA,$TERROR,$FELIZ,$MISTERIO]}"
pedir POST /api/pistas 400 '{"nombre":"Inexistente","etiquetaIds":[99999]}'
pedir GET "/api/pistas?etiqueta=terror" 200 ""
comprobar "filtro por etiqueta" 'length == 2'
pedir GET "/api/pistas?buscar=faro" 200 ""
comprobar "búsqueda de pistas" 'length == 1'
pedir PUT "/api/pistas/$PISTA_CALMA" 200 "{\"nombre\":\"Puerto en calma\",\"artista\":\"Orquesta Andina\",\"etiquetaIds\":[$CALMA,$FELIZ]}"
comprobar "etiquetas de la pista actualizadas" '[.etiquetas[].nombre] | sort == ["calma", "feliz"]'

echo "== Inmersión con IA"
pedir POST "/api/capitulos/$CAPITULO/inmersion" 400 ""
pedir POST "/api/capitulos/$CAPITULO/inmersion" 200 "" "X-Gemini-Api-Key: clave-de-prueba"
comprobar "cuatro escenas generadas" '.cantidadEscenas == 4'
comprobar "la primera escena empieza en 0" '.escenas[0].posicionInicio == 0'
comprobar "escenas en orden de lectura" '[.escenas[].posicionInicio] == ([.escenas[].posicionInicio] | sort)'
comprobar "cada escena tiene etiquetas" 'all(.escenas[]; (.etiquetas | length) >= 1)'
ESCENA_CALMA="$(campo '.escenas[] | select(.etiquetas[].nombre == "calma") | .id')"
pedir GET "/api/capitulos/$CAPITULO" 200 ""
comprobar "capítulo marcado como procesado" '.procesadoIa == true and .cantidadEscenas == 4'
pedir GET "/api/capitulos/$CAPITULO/escenas" 200 ""
comprobar "listado de escenas" 'length == 4'
pedir GET "/api/escenas/$ESCENA_CALMA/recomendaciones" 200 ""
comprobar "recomienda la pista de calma" '.[0].pista.nombre == "Puerto en calma" and .[0].afinidad == 1'
pedir GET "/api/capitulos/$CAPITULO/inmersion" 200 ""
comprobar "plan de inmersión con pistas" '(.escenas | length) == 4 and ([.escenas[] | select(.pistaRecomendada != null)] | length) >= 2'
pedir POST "/api/capitulos/$EPILOGO/inmersion" 422 "" "X-Gemini-Api-Key: clave-de-prueba"

echo "== Escenas manuales"
pedir POST "/api/capitulos/$CAPITULO/escenas" 409 "{\"posicionInicio\":0,\"etiquetaIds\":[$CALMA]}"
pedir POST "/api/capitulos/$CAPITULO/escenas" 201 "{\"posicionInicio\":10,\"etiquetaIds\":[$CALMA,$INTENSO]}"
comprobar "orden recalculado por posición" '.orden == 2'
pedir POST "/api/capitulos/$CAPITULO/escenas" 400 "{\"posicionInicio\":5000,\"etiquetaIds\":[$CALMA]}"
pedir POST "/api/capitulos/$CAPITULO/escenas" 400 "{\"posicionInicio\":20,\"etiquetaIds\":[$INTENSO]}"

echo "== Integridad"
pedir DELETE "/api/etiquetas/$CALMA" 409 ""
pedir PUT "/api/capitulos/$CAPITULO" 200 '{"titulo":"La tormenta","contenido":"Texto nuevo del capítulo."}'
comprobar "al cambiar el texto se borran las escenas" '.procesadoIa == false and .cantidadEscenas == 0'
pedir DELETE "/api/capitulos/$CAPITULO/escenas" 204 ""
pedir DELETE "/api/pistas/$PISTA_TERROR" 204 ""
pedir DELETE "/api/libros/$LIBRO" 204 ""
pedir GET "/api/capitulos/$EPILOGO" 404 ""
pedir GET "/api/libros/$LIBRO" 404 ""

echo "== Documentación"
pedir GET /swagger/v1/swagger.json 200 ""
comprobar "Swagger incluye la generación de inmersión" '.paths | has("/api/capitulos/{id}/inmersion")'

echo
echo "Prueba de humo completada sin errores."
