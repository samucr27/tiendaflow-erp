# Guion del video: tour de arquitectura (máximo 1 minuto)

Navegar en Notion por arc42: Sección 5, luego Sección 10.

**[0:00 - 0:10] Sección 5, vista general.** "TiendaFlow es un monolito modular
en cuatro capas: Domain, Application, Infrastructure y Web. Domain y
Application no dependen de EF Core."

**[0:10 - 0:30] Sección 5, nivel 2: nómina.** "La guía habla de un
microservicio de nómina; lo implementamos como un módulo dentro del monolito,
con su propio motor, `PayrollEngine`, y su endpoint
`POST /api/nomina/calculate`. El motor solo depende de interfaces: estrategias
por tipo de contrato, calculadora de retención y proveedor de parámetros.
Esto aplica SRP y DIP."

**[0:30 - 0:50] Sección 10, roles.** "En la sección 10 está la matriz de siete
roles por permiso. Ese endpoint exige el permiso `hr.payroll.manage`, que solo
tiene el Oficial de RR. HH. La autorización se aplica en tres niveles: página
o endpoint, interfaz y servicio, y los accesos denegados se auditan."

**[0:50 - 1:00] Cierre.** "La coherencia es esta: el componente de nómina de
la sección 5 está protegido por el modelo de roles de la sección 10."