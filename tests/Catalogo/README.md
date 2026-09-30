# Pruebas de catálogo y producción

Requieren PostgreSQL local en `127.0.0.1:55459`, usuario `postgres`, con permiso para crear bases de datos. No usan secretos ni conexiones de producción.

```powershell
dotnet run --project tests/Catalogo/Catalogo.csproj
```

Crean una base descartable, aplican las migraciones y comprueban los empaques predeterminados, las modificaciones manuales de pedidos, los totales por formato y las subrecetas anidadas (costos, producción, consumo de insumos y rechazo de referencias circulares). La base se elimina al terminar.

Para revisar las pantallas con los mismos datos de prueba:

```powershell
dotnet run --project tests/Catalogo/Catalogo.csproj -- --preview
```

La vista previa escucha exclusivamente en `http://127.0.0.1:5087`, con un administrador de prueba. Detenerla al terminar; este anfitrión es solo para verificación local.
