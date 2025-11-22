# Checklist de Arte v0

## 0. Metadatos
- [ ] Autor/a
- [ ] Fecha
- [ ] Categoría (characters/enemies/items/environments/ui/fx)
- [ ] Subcategoría (main/npc/basic/boss/foreground/midground/background/…)
- [ ] Nombre de archivo definitivo
- [ ] Versión (v0, v1, …)
- [ ] Enlace a fuente/brief

---

## 1. Especificaciones técnicas
- [ ] Resolución exacta: ______ x ______ px
- [ ] DPI (si aplica): ______
- [ ] Perfil de color: ______
- [ ] Formato (PNG/JPEG/SVG/PSD/EXR/…): ______
- [ ] Fondo (transparente/sólido): ______
- [ ] Bordes sin recorte / padding de seguridad: ______
- [ ] Nomenclatura de archivo (`categoria_subcategoria_nombre_vX.ext`): ______
- [ ] Peso de archivo dentro de límite: ______ MB

---

## 2. Coherencia visual (según “Guía de Estilo IA v0”)
- [ ] Paleta: usa solo colores aprobados
- [ ] Línea: grosor y tratamiento correctos
- [ ] Proporciones: dentro de rangos definidos
- [ ] Tono: coincide con descriptores establecidos
- [ ] Consistencia con piezas del mismo lote

---

## 3. Usabilidad en motor (Unity)
- [ ] Export adecuado (atlas/sprite único/tileset): ______
- [ ] Modo de compresión recomendado: ______
- [ ] Pivot/anchor correctos: ______
- [ ] Margen para slicing / 9-slice (si aplica): ______
- [ ] Sin artefactos en escalado 0.75×–2×
- [ ] Colisión/oclusiones (si aplica) definidas

---

## 4. Accesibilidad y UI (si aplica)
- [ ] Contraste mínimo texto/fondo alcanzado
- [ ] Tamaño mínimo legible en destino
- [ ] Estados (hover/pressed/disabled/active) provistos
- [ ] Iconografía reconocible a 24–32 px

---

## 5. Integridad de datos
- [ ] Metadatos en `data/metadata/*.json` actualizados
- [ ] Etiquetas según `data/taxonomy.json`
- [ ] Licencia/fuente registrada y válida
- [ ] Miniaturas en `assets/temp` o `outputs/previews`

---

## 6. Revisión y aprobación
- [ ] Revisión arte (responsable): ______ / fecha: ______
- [ ] Revisión técnica (responsable): ______ / fecha: ______
- [ ] Observaciones: ______________________________
- [ ] Estado: Aprobado / Cambios / Rechazado
