# Validación de la distribución — SecureCredentials.Manager 1.0.1

**Fecha:** 28 de octubre de 2026  
**Estado:** Validación funcional satisfactoria en los entornos probados  
**Producto:** SecureCredentials.Manager 1.0.1  
**Biblioteca:** SecureCredentials 1.0.0  
**Plataforma del paquete:** Linux x64 (`linux-x64`)  
**Formato de archivo cifrado:** `.enc` v2 (sin cambios)

## 1. Objetivo y alcance

Documentar las evidencias disponibles sobre la compilación, el empaquetado, la integridad y el funcionamiento de SecureCredentials.Manager 1.0.1 en un equipo de desarrollo y en un segundo equipo con RebornOS/Arch Linux y Cinnamon/X11.

La validación corresponde a los equipos y configuraciones indicados. **No constituye una certificación universal** para todas las distribuciones Linux ni una auditoría de seguridad criptográfica.

## 2. Entregables

| Entregable | Archivo | Versión |
| --- | --- | --- |
| Aplicación gráfica portable Linux x64 | `SecureCredentials.Manager-1.0.1-linux-x64.tar.gz` | 1.0.1 |
| Biblioteca NuGet | `SecureCredentials.1.0.0.nupkg` | 1.0.0 |
| Manifiesto de integridad | `SHA256SUMS-linux-x64.txt` | — |

El paquete de la aplicación incluye el ejecutable `SecureCredentials.Manager` y el lanzador alternativo `securecredentials-manager.sh`.

## 3. Fabricación y pruebas automatizadas

La ejecución de `./fabricar_entregables.sh` en el equipo de desarrollo informó de:

- Compilación Release de la solución: **correcta**.
- Pruebas xUnit: **106 ejecutadas, 106 correctas, 0 fallidas, 0 omitidas**.
- Generación del paquete NuGet de la biblioteca 1.0.0: **correcta**.
- Publicación de Manager 1.0.1 para `linux-x64`: **correcta**.
- Generación del archivo `.tar.gz` y del manifiesto SHA-256: **correcta**.

Estas pruebas automatizadas no sustituyen las comprobaciones manuales de la interfaz gráfica.

## 4. Equipo de desarrollo

**Entorno comunicado:** RebornOS/Arch Linux, Cinnamon/X11, configuración regional `es_ES.UTF-8`.

Se detectó que, con `XMODIFIERS=@im=ibus`, la entrada directa de vocales acentuadas mediante teclas muertas fallaba en el editor de Avalonia, aunque el pegado de Unicode funcionaba. Se comprobó que la entrada de acentos funcionaba al iniciar el Manager con:

```bash
env XMODIFIERS=@im=none GTK_IM_MODULE=xim QT_IM_MODULE=xim \
  ./SecureCredentials.Manager
```

Por ello se incorporó `securecredentials-manager.sh`, que aplica esas variables **solo al proceso de la aplicación**. Esta solución de compatibilidad no cambia la configuración global del sistema.

La fabricación de la versión 1.0.1 y las 106 pruebas automatizadas se realizaron satisfactoriamente en este entorno.

## 5. Segundo equipo: jra-nuc7i3bnh

### 5.1. Entorno observado

| Parámetro | Valor |
| --- | --- |
| Identificador de equipo | `jra-nuc7i3bnh` |
| Arquitectura | `x86_64` |
| Sistema operativo informado | Arch Linux (equipo RebornOS) |
| Sesión gráfica | `x11` |
| Escritorio | `X-Cinnamon` |
| Idioma / locale | `es_ES.UTF-8` |
| Variable `XMODIFIERS` | Vacía |
| Carpeta de pruebas | `/tmp/securecredentials-1.0.1` |

### 5.2. Resultados confirmados

| Prueba | Resultado | Evidencia comunicada |
| --- | --- | --- |
| Extracción del paquete | **Correcto** | Ejecutable y lanzador presentes en `/tmp/securecredentials-1.0.1` |
| Permisos de ejecución | **Correcto** | Ambos archivos mostraron `-rwxr-xr-x` |
| Verificación SHA-256 del `.tar.gz` | **Correcto** | Confirmación explícita del usuario |
| Arranque directo del ejecutable | **Correcto** | La ventana abre |
| Escritura de acentos en arranque directo | **Correcto** | Confirmación explícita del usuario |
| Arranque mediante lanzador alternativo | **Correcto** | La ventana abre |
| Escritura de acentos mediante lanzador | **Correcto** | Confirmación explícita del usuario |
| Funcionamiento general de la aplicación | **Correcto** | Confirmación final del usuario: «Sí funciona bien la aplicación» |

La confirmación de funcionamiento general se interpreta como aceptación funcional de la aplicación en el segundo equipo, incluido el flujo de edición, cifrado y recuperación comentado durante la prueba. **No se adjuntaron trazas individuales ni una comparación automática del texto original y el recuperado.**

## 6. Conclusiones

1. La distribución portable de **SecureCredentials.Manager 1.0.1 para Linux x64** se compiló y empaquetó satisfactoriamente.
2. La suite automatizada disponible terminó con **106/106 pruebas superadas**.
3. La integridad del archivo distribuido se comprobó mediante **SHA-256 en el segundo equipo**.
4. El Manager arrancó y admitió caracteres acentuados **con y sin lanzador** en `jra-nuc7i3bnh`.
5. El problema de composición de acentos observado en el equipo de desarrollo se resolvió mediante el lanzador alternativo; **no se reprodujo** en el segundo equipo.
6. El usuario confirmó el funcionamiento correcto de la aplicación en el segundo equipo.

**Dictamen:** **VALIDACIÓN FUNCIONAL SATISFACTORIA** para la distribución Linux x64 en los dos entornos RebornOS/Cinnamon/X11 probados, con la salvedad de que el primer equipo requiere el lanzador de compatibilidad para la entrada de acentos.

## 7. Límites y acciones futuras

- No se han documentado pruebas en Windows, Wayland ni otras distribuciones Linux.
- La solución `XMODIFIERS=@im=none` puede interferir con métodos de entrada que dependan de IBus; se conserva el arranque directo como alternativa.
- La compatibilidad en un equipo limpio sin bibliotecas nativas adicionales no queda demostrada por estas pruebas.
- No se han incluido en este informe hashes SHA-256 literales, ya que no se facilitaron sus valores; consta la confirmación de coincidencia.
- Si se requiere una certificación formal más estricta, conservar capturas, registros de ejecución, hashes y una prueba automatizada de ida y vuelta de los archivos `.enc`.

## 8. Reproducibilidad

Desde el directorio que contiene los archivos distribuidos:

```bash
# Comprobar exclusivamente el archivo del Manager
grep 'SecureCredentials.Manager-1.0.1-linux-x64.tar.gz' \
  SHA256SUMS-linux-x64.txt | sha256sum -c -

# Extraer
mkdir -p /tmp/securecredentials-1.0.1
tar -xzf SecureCredentials.Manager-1.0.1-linux-x64.tar.gz \
  -C /tmp/securecredentials-1.0.1

# Arranque directo
/tmp/securecredentials-1.0.1/SecureCredentials.Manager

# Arranque con compatibilidad de teclado
/tmp/securecredentials-1.0.1/securecredentials-manager.sh
```

---

**Registro preparado a partir de las salidas de compilación y de las comprobaciones comunicadas durante las pruebas del 28 de octubre de 2026.**
