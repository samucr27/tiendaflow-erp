# Documentación final de TiendaFlow

Esta carpeta reúne la documentación de la entrega final, escrita sobre la rama
`feature/erp-final`. Los documentos congelados de entregas anteriores
(`docs/sprint2`, `docs/notion` y `docs/arc42`) no se modificaron.

## Índice

| Documento | Contenido |
| --- | --- |
| [implemented-modules.md](implemented-modules.md) | Módulos y pantallas completados en esta fase, con sus rutas y permisos |
| [permissions.md](permissions.md) | Cómo se aplica la matriz de permisos y qué permisos usa cada módulo |
| [pending-items.md](pending-items.md) | Auditoría de lo que queda pendiente y por qué no se implementó |
| [testing.md](testing.md) | Resultados de compilación, pruebas y cobertura |
| [database.md](database.md) | Migraciones, sembrado y comandos de base de datos |
| [deploy-azure.md](deploy-azure.md) | Guía de aprovisionamiento y despliegue en Azure |

## Alcance de esta entrega

- Compras: órdenes de compra con creación, envío, aprobación, recepción a
  inventario y anulación; ranking y evaluación de proveedores.
- Panel: indicadores de ventas del día y órdenes de compra, filtrados por
  permiso.
- Reportes: inventario, ventas y nómina, con exportación CSV.
- CRM: segmentación A/B/C de clientes por volumen de compra.
- Facturación: exportación de un XML **simulado** de factura electrónica.
- Infraestructura: Terraform para Azure SQL y App Service Linux, guía de
  despliegue y flujo de trabajo de validación.

> El XML electrónico, la segmentación A/B/C y los parámetros de nómina son
> referencias académicas. No son integraciones legales ni configuraciones
> certificadas; cada documento lo indica en el punto correspondiente.
