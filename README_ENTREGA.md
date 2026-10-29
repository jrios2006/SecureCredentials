# SecureCredentials 1.0.0 - Kit de fabricacion actualizado

Incluye:
- `fabricar_entregables.sh`: compilacion, pruebas, NuGet, publicacion portable y SHA-256.
- `securecredentials-manager.sh`: lanzador alternativo Linux para teclas muertas con X11/IBus.
- `GUIA_FABRICACION_ENTREGABLES.md`: guia de fabricacion y validacion.

Coloca ambos scripts juntos en la raiz del repositorio y ejecuta:

```bash
chmod +x fabricar_entregables.sh
./fabricar_entregables.sh linux-x64
```

El archivo NuGet no se modifica por el ajuste del lanzador. El paquete Linux
incluye el ejecutable directo y el lanzador alternativo.
