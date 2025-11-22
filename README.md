# Proyecto TIROLEI

## Descripción general
Tirolei es un proyecto de integración entre arte, inteligencia artificial y desarrollo de videojuegos. Su objetivo es construir un ecosistema completo de diseño visual, generación de datasets, entrenamiento de modelos y despliegue en Unity, manteniendo coherencia estética, técnica y narrativa.

---

## Estructura de carpetas

Tirolei/
│
├── docs/
│   ├── guia_estilo_ia/
│   ├── data_design_doc_v0.docx
│   └── roadmap/
│
├── data/
│   ├── raw/
│   │   ├── characters/
│   │   │   ├── main/
│   │   │   └── npc/
│   │   ├── enemies/
│   │   │   ├── basic/
│   │   │   └── bosses/
│   │   ├── items/
│   │   ├── elements/
│   │   ├── environments/
│   │   │   ├── foreground/
│   │   │   ├── midground/
│   │   │   └── background/
│   │   ├── ui/
│   │   └── fx/
│   ├── processed/
│   ├── metadata/
│   │   ├── characters.json
│   │   ├── enemies.json
│   │   ├── items.json
│   │   ├── environments.json
│   │   ├── fx.json
│   │   └── ui.json
│   ├── checklists/
│   └── taxonomy.json
│
├── ml/
│   ├── pipelines/
│   │   ├── style_ft/
│   │   └── animation_ft/
│   ├── tools/
│   └── outputs/
│
├── unity/
│   ├── Assets/
│   │   └── Tirolei/
│   │       ├── Art/
│   │       ├── Scenes/
│   │       ├── Scripts/
│   │       ├── Prefabs/
│   │       └── Materials/
│   └── StreamingAssets/
│       └── tirolei_io/
│
├── assets/
│   ├── approved/
│   │   ├── characters/
│   │   ├── enemies/
│   │   ├── environments/
│   │   ├── items/
│   │   ├── fx/
│   │   └── ui/
│   └── temp/
│
├── drafts/
│   ├── sketches/
│   ├── concepts/
│   └── references/
│
├── qa/
│   ├── tests_gameplay/
│   ├── tests_style/
│   ├── tests_data/
│   └── reports/
│
└── README.md

---

## Documentos clave

- **guia_estilo_ia/** — Guías visuales, tono y coherencia artística.  
- **data_design_doc_v0.docx** — Documento de diseño de datos inicial.  
- **roadmap/** — Planificación, milestones y dependencias.

---

## Flujo general

1. Creación y curación de datasets en `data/raw/`.  
2. Procesamiento y metadatos en `data/processed/` y `data/metadata/`.  
3. Entrenamiento de modelos y pipelines en `ml/`.  
4. Integración de resultados aprobados en `assets/approved/`.  
5. Exportación al entorno Unity.  
6. Validación y pruebas en `qa/`.

---

## Convenciones

- Nombres en minúsculas con guiones bajos.  
- Archivos de texto en formato Markdown o JSON.  
- Los datasets incluyen metadatos de fuente, licencia y clasificación.  
- Las carpetas `approved/` contienen solo material validado por arte y QA.

---

## Mantenimiento

Las modificaciones estructurales se reflejan en este README.  
El progreso, experimentos o iteraciones se registran en los documentos dentro de `docs/roadmap/`.