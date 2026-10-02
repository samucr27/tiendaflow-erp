---
title: "TiendaFlow: ERP System for Convenience-Store Chains"
---

# TiendaFlow: ERP System for Convenience-Store Chains

![TiendaFlow logo](docs/img/logo.png)

**Project Definition (Sprint 0) and Requirements, Product Backlog, UML Models and Prototype (Sprint 1)**

CADI: Software Engineering II, Systems and Computer Engineering Program, Faculty of Engineering, University of Cundinamarca. Instructor: Carlos Eduardo Mujica Reyes.

| Team member | Role |
|---|---|
| Sergio Steeven Moreno Forero | Project Manager and Scrum Master |
| Samuel Castellanos Rivera | Software Architect and Backend Developer |
| Juan Sebastian Rodriguez Blanco | Frontend Developer and UI/UX Designer |
| Jonathan Alejandro Yacuma Rivera | Database Administrator and QA Tester |

## Project resources

| Resource | Link |
|---|---|
| Complete project document (PDF, APA 7) | [TiendaFlow_Project_Definition_Sprint1.pdf](docs/TiendaFlow_Project_Definition_Sprint1.pdf) |
| Figma design file (design system, mockups and interactive prototype) | [ERP_Mockups_TiendaFlow](https://www.figma.com/design/3LW57RjiNETxaukic2S6JE) |
| Prototype generated with Figma Make | [early-hope-36544320.figma.site](https://early-hope-36544320.figma.site/) |
| Google Colab notebook with all the PlantUML diagrams | [ERP_PlantUML_TiendaFlow](https://colab.research.google.com/drive/1VtObCkwC1xnCBrrpza1oSu0oOpki-RqI?usp=sharing) |
| PlantUML source files | [docs/uml](docs/uml) |
| Repository | [samucr27/tiendaflow-erp](https://github.com/samucr27/tiendaflow-erp) |

## Contents

1. [Sprint 0: Project definition](#sprint-0-project-definition)
2. [Sprint 1: Requirements, Product Backlog, UML models and prototype](#sprint-1-requirements-product-backlog-uml-models-and-prototype)
3. [Repository structure](#repository-structure)

# Sprint 0: Project definition

## Problem and proposed solution

Convenience-store chains operate many distributed points of sale with extended or 24/7 hours, high product turnover and perishable goods. In small and medium chains, inventory, sales, purchasing, staff and customer information are usually managed in separate tools, which causes stockouts, overstock, expired products, long queues and low customer loyalty.

**TiendaFlow** is a modular web ERP built only with Microsoft technologies (ASP.NET Core, Blazor and SQL Server). It integrates six modules on a single platform: five business modules (Inventory, Sales and Billing, Supplier and Purchasing, Human Resources, and Customer Management) and a cross-cutting Security and Reporting module, with DIAN electronic invoicing, an offline-capable POS and real-time consolidated reports.

## Project management

The project follows Scrum during the 16 weeks of the semester (12 hours per week per member, 768 hours in total).

| Sprint | Weeks | Objective |
|:---:|:---:|---|
| 0 | 1 to 2 | Project definition and planning |
| 1 | 3 to 4 | Requirements, UML modeling and prototype |
| 2 | 5 to 7 | Security base and Inventory module |
| 3 | 8 to 10 | Sales and Billing module |
| 4 | 11 to 12 | Supplier and Purchasing module |
| 5 | 13 to 14 | Human Resources and CRM modules |
| 6 | 15 to 16 | Reporting, integration and delivery |

![Project schedule](docs/img/gantt.png)

| Cost concept | COP |
|---|---:|
| Personnel (768 hours) | 25,536,000 |
| Microsoft licensing for commercial development and pilot | 19,295,850 |
| Other costs (internet and electricity) | 1,600,000 |
| Contingency reserve (10%) | 4,643,185 |
| **Total project cost** | **51,075,035** (about USD 16,426.54) |

Commercial model: subscription of **COP 190,000 per store per month** (up to five users, no transaction fees). Break-even point: about **269 store-months**.

## Competitor analysis

| Application | Cost | Main gap for convenience stores |
|---|---|---|
| SAP Business One | USD 110 to 219 per user per month (cloud) | High cost per user and implementation; basic HR |
| Odoo | About USD 24.90 to 61 per user per month | Cost grows with each cashier; partner dependency |
| Square for Retail | USD 0 to 149 per location plus processing fees | Not available in Colombia; no DIAN invoicing, suppliers or payroll |
| Alegra | COP 74,900 to 319,900 per month | Accounting orientation; no FEFO, 24/7 shifts or supplier portal |
| **TiendaFlow** | **COP 190,000 per store per month** | Preconfigured for convenience stores |

## Product identity and technologies

Palette: red-orange **#E8412C** (primary), charcoal **#2B2B2B** (secondary) and light gray **#F5F5F5** (background).

| Category | Technology |
|---|---|
| Backend | ASP.NET Core (C#) Web API and Worker Service |
| Frontend | Blazor WebAssembly (POS) and Blazor Server (back office and supplier portal) |
| Database | SQL Server 2022 with Entity Framework Core |
| IDE and testing | Visual Studio; xUnit with coverlet, Postman and Accessibility Insights |
| Version control, CI and documentation | Git, GitHub, GitHub Actions and GitHub Pages |
| Modeling | PlantUML (Visual Studio Code and Google Colab) |
| Prototyping | Figma and Figma Make (prescribed by the Sprint 1 guide) |

# Sprint 1: Requirements, Product Backlog, UML models and prototype

## Modules of the guide and modules of TiendaFlow

| Module of the guide | Module of TiendaFlow |
|---|---|
| Purchasing (Compras) | Supplier and Purchasing Management |
| Inventory (Inventarios) | Inventory Management |
| Suppliers (Proveedores) | Supplier and Purchasing Management |
| Human resources (Talento Humano) | Human Resources Management |
| CRM (Clientes) | Customer Management (CRM) |
| Billing and sales (Facturación y Ventas) | Sales and Billing Management |
| (not in the guide) | Security and Reporting |

Every module covers the four CRUD operations (create, read, update and delete). Functional requirements follow IEEE 830 with an acceptance criterion; non-functional requirements follow ISO/IEC 25010 with a verification metric.

## Functional decomposition tree and general use cases

![Functional decomposition tree of TiendaFlow](docs/img/tree_overall.png)

![General use case diagram of TiendaFlow](docs/img/uc_overall.png)

## Requirements and Product Backlog summary

| Epic | Module | RF | RNF | User stories | Story points | Must story points | Planned sprint |
|---|---|:---:|:---:|:---:|:---:|:---:|---|
| E-01 | Inventory Management | 11 | 3 | 11 | 51 | 29 | Sprint 2 |
| E-02 | Sales and Billing Management | 10 | 5 | 10 | 55 | 39 | Sprint 3 |
| E-03 | Supplier and Purchasing Management | 10 | 2 | 10 | 44 | 16 | Sprint 4 |
| E-04 | Human Resources Management | 8 | 3 | 8 | 46 | 16 | Sprint 5 |
| E-05 | Customer Management (CRM) | 10 | 2 | 10 | 35 | 11 | Sprint 5 |
| E-06 | Security and Reporting | 8 | 2 | 8 | 35 | 12 | Sprints 2 and 6 |
| **Total** | | **57** | **17 + 15 system-wide** | **57** | **266** | **123** | |

User stories are prioritized with MoSCoW and estimated with Planning Poker (Fibonacci scale). The 123 story points of the *Must* stories form the minimum viable product. The 32 non-functional requirements cover the eight ISO/IEC 25010 categories with at least two requirements each.

### Definition of Done

1. Code reviewed by at least one teammate through a pull request on GitHub.
2. Unit tests with xUnit and coverage of the business logic of 80% or more.
3. Integration tests of the API endpoints executed (Postman collection).
4. API documentation updated in Swagger (OpenAPI).
5. Architecture documentation (arc42) and PlantUML diagrams updated on this site.
6. Database changes versioned with Entity Framework Core migrations.
7. Deployment pipeline (GitHub Actions) reviewed and versioned in GitHub.
8. Create, update and delete operations recorded in the audit log.
9. Feature deployed in the test environment and accepted by the Product Owner.

## Modules

### 1. Inventory Management (epic E-01)

Product catalog, real-time stock per store (each store is its warehouse), receipts with batch and expiration date, minimum stock and FEFO alerts, transfers between stores, physical counts and inventory reports.

| Functional requirements | Non-functional requirements | User stories | Story points | Must story points | Planned sprint |
|:---:|:---:|:---:|:---:|:---:|:---|
| 11 | 3 | 11 | 51 | 29 | Sprint 2 |

![Functional decomposition tree of the Inventory Management module](docs/img/tree_inv.png)

![Use case diagram of the Inventory Management module](docs/img/uc_inv.png)

### 2. Sales and Billing Management (epic E-02)

Point of sale with barcode scanning, split payments, promotions and offline mode; DIAN electronic invoices and POS documents with status tracking; returns and voided sales with credit notes; cash register opening, closing and reconciliation.

| Functional requirements | Non-functional requirements | User stories | Story points | Must story points | Planned sprint |
|:---:|:---:|:---:|:---:|:---:|:---|
| 10 | 5 | 10 | 55 | 39 | Sprint 3 |

![Functional decomposition tree of the Sales and Billing Management module](docs/img/tree_ven.png)

![Use case diagram of the Sales and Billing Management module](docs/img/uc_ven.png)

### 3. Supplier and Purchasing Management (epic E-03)

Supplier registry and price lists, reorder suggestions, purchase orders with approval, sending and cancellation, supplier portal for order confirmation, purchase history and supplier evaluation.

| Functional requirements | Non-functional requirements | User stories | Story points | Must story points | Planned sprint |
|:---:|:---:|:---:|:---:|:---:|:---|
| 10 | 2 | 10 | 44 | 16 | Sprint 4 |

![Functional decomposition tree of the Supplier and Purchasing Management module](docs/img/tree_sup.png)

![Use case diagram of the Supplier and Purchasing Management module](docs/img/uc_sup.png)

### 4. Human Resources Management (epic E-04)

Employee records and contracts managed by the HR officer, terminations with a final settlement report, 24/7 shift planning and swaps, clock-in and clock-out attendance, and the basic payroll pre-settlement.

| Functional requirements | Non-functional requirements | User stories | Story points | Must story points | Planned sprint |
|:---:|:---:|:---:|:---:|:---:|:---|
| 8 | 3 | 8 | 46 | 16 | Sprint 5 |

![Functional decomposition tree of the Human Resources Management module](docs/img/tree_rh.png)

![Use case diagram of the Human Resources Management module](docs/img/uc_rh.png)

### 5. Customer Management (CRM) (epic E-05)

Customers with data-processing consent (Law 1581 of 2012), data update and anonymization, loyalty points and tiers, segmented campaigns by email and SMS, purchase history, recommendations and PQRS.

| Functional requirements | Non-functional requirements | User stories | Story points | Must story points | Planned sprint |
|:---:|:---:|:---:|:---:|:---:|:---|
| 10 | 2 | 10 | 35 | 11 | Sprint 5 |

![Functional decomposition tree of the Customer Management (CRM) module](docs/img/tree_crm.png)

![Use case diagram of the Customer Management (CRM) module](docs/img/uc_crm.png)

### 6. Security and Reporting (epic E-06)

Users and roles per store, two-factor authentication for administrative roles, immutable audit log, system parameters, and consolidated reports for the corporate management.

| Functional requirements | Non-functional requirements | User stories | Story points | Must story points | Planned sprint |
|:---:|:---:|:---:|:---:|:---:|:---|
| 8 | 2 | 8 | 35 | 12 | Sprints 2 and 6 |

![Functional decomposition tree of the Security and Reporting module](docs/img/tree_seg.png)

![Use case diagram of the Security and Reporting module](docs/img/uc_seg.png)

## Architecture and data models

### Class diagram of the Inventory module

![Class diagram of the Inventory module](docs/img/class_inventory.png)

### Entity-relationship diagram (19 entities)

![Entity-relationship diagram](docs/img/er_tiendaflow.png)

### C4 model

![C4 context diagram](docs/img/c4_context.png)

![C4 container diagram](docs/img/c4_container.png)

![C4 component diagram of the API](docs/img/c4_component.png)

## Mockups and interactive prototype

The Figma file [ERP_Mockups_TiendaFlow](https://www.figma.com/design/3LW57RjiNETxaukic2S6JE) contains the design system, the three mockups requested in the guide and a navigable prototype of the six modules with 31 screens and 38 dialogs. Open it and press **Present** to start from the login screen.

![Design system](docs/img/figma_design_system.png)

![Purchases dashboard](docs/img/figma_purchases.png)

![Inventory dashboard](docs/img/figma_inventory.png)

![Payroll dashboard](docs/img/figma_hr_payroll.png)

Flow requested in the guide (create an order, see the result and save it):

1. Sign in, verify the second factor and open **Purchases**.
2. Press **Create order**, complete the order and press **Review order**.
3. Check the result and press **Save order**.
4. Verify that order PO-2026-052 appears at the top of the list with the status Sent.

![Screens 1 to 16 of the prototype](docs/img/figma_map_a.png)

![Screens 17 to 31 of the prototype](docs/img/figma_map_b.png)

The first functional prototype, generated with [Figma Make](https://early-hope-36544320.figma.site/), includes Inventory, Sales (POS), Suppliers and Employees screens that share the same product catalog.

![Figma Make prototype: POS](docs/img/screen_pos.png)

## Reflection questions

1. **Why define acceptance criteria before developing a user story?** They turn the story into something testable and give the team, the Product Owner and the testers the same definition of done. For example, HU-VEN-02 requires the product and the total to appear in 2 seconds or less and the stock to decrease when the sale is confirmed, which drives the design of the POS endpoint and its load test.
2. **How does PlantUML help keep the architecture documentation up to date?** Diagrams are plain text versioned in Git and reviewed in pull requests; editing one line regenerates the diagram with a consistent style; and it integrates with Visual Studio Code, Google Colab, GitHub Pages and continuous integration.
3. **What advantages does Figma Make offer for rapid ERP prototyping?** It generates interactive screens from natural-language prompts, so flows can be validated with users in minutes, while the designer keeps control of the design system, accessibility and consistency.

# Repository structure

| Path | Content |
|---|---|
| `index.md` | This documentation page (GitHub Pages) |
| `docs/TiendaFlow_Project_Definition_Sprint1.pdf` | Complete project document |
| `docs/uml/` | PlantUML sources of all the diagrams (`.puml`) |
| `docs/img/` | Rendered diagrams, mockups and prototype screens |
