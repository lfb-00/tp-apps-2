# Catálogo de repuestos — fuentes

Cada repuesto de `src/RepMatch.Persistence/DatosSemilla.cs` sale de un catálogo oficial publicado
por una automotriz o por un fabricante de autopartes. Nada está inventado: si una pieza no tiene
fuente, no está en el catálogo. Por eso el catálogo cubre diez familias de vehículos y, en cada
una, solo los tipos de pieza que esas fuentes documentan.

Relevamiento hecho el 2026-11-15. Los valores se leyeron de las páginas de cada PDF y se
contrastaron con la imagen de la página, no solo con el texto extraído.

## Fuentes

| Id | Documento | Editor | Edición | URL |
|---|---|---|---|---|
| VW | *Linha Economy* — catálogo de piezas | Volkswagen do Brasil | Edición 7, febrero de 2020 | [vw.com.br](https://www.vw.com.br/idhub/content/dam/onehub_pkw/importers/br/pecas-linha_economy/pdf/20200203_Catalogo_Linha_Economy_Fevereiro_2020(1).pdf) |
| BF | *Catálogo de Filtros — Linha Leve* | Robert Bosch (Autopeças Bosch, Brasil) | 2019 \| 2020 | [boschaftermarket.com](https://www.boschaftermarket.com/xrm/media/images/country_specific/br/downloads_19/pdf_9/catlogo_filtros_2019_linha_leve.pdf) |
| BV | *Velas, Cabos e Bobinas de Ignição* | Robert Bosch (Autopeças Bosch, Brasil) | 2019 \| 2020 | [boschaftermarket.com](https://www.boschaftermarket.com/xrm/media/images/country_specific/br/downloads_19/pdf_9/catlogo_velas_cabos_bobinas_2019.pdf) |
| MH | *Catálogo de Filtros* | MAHLE Aftermarket (México) | 2025 | [mahle-aftermarket.com](https://www.mahle-aftermarket.com/media/local-media-north-america/mexico/catalogos_espa_ol/catalogo_de_filtros_2025.pdf) |
| MU | *Main Applications for Utes and Light Commercial Vehicles* | MANN+HUMMEL (MANN-FILTER Australia) | 01/2024 | [mann-filter.com](https://www.mann-filter.com/content/dam/website/mann-filter/mann-filter-com/australia/download-hub/document/Mann_Filter_Utes_LCV_Guide.pdf) |
| MI | *Informativo Técnico — Linha Leve* (C 33 017/1, C 27 030/1) | MANN-FILTER Brasil | 10/2021 y 11/2021 | [mann-filter.com](https://www.mann-filter.com/content/dam/website/mann-filter/mann-filter-com/br-pt/documents/informativos/Informativo%20Tecnico%2011.21%20-%20Linha%20Leve.pdf) |
| MC | Catálogo en línea, ficha de C 33 017/1 | MANN-FILTER Brasil | actualizada 2026-03-23 | [mann-filter.com](https://www.mann-filter.com/br-pt/catalogo/resultados-da-pesquisa/product.html/c33017/1_mann-filter.html) |
| ML | *Linha Leve* — folheto de aplicações (filtros de ar, óleo, combustível e cabine) | MANN-FILTER Brasil | 02/2022 | [mann-filter.com](https://www.mann-filter.com/content/dam/website/mann-filter/mann-filter-com/br-pt/documents/folheto_linha_leve_592x210mm_fev22_WEB.pdf) |

El catálogo de Volkswagen da el número original de la automotriz (*Ref. série*), el de su línea
económica (*Ref. Economy*) y equivalencias de reposición que la propia VW publica (Cobreq, Fremax,
Fram, Tecfil, Bosch). Los de Bosch dan el código de motor y el período de fabricación de cada
aplicación. El de MAHLE es del mercado mexicano y no trae código de motor: sus aplicaciones se
toman por modelo, cilindrada y año, y por eso solo se usó donde el motor coincide con el que se
vende en Argentina (Ranger diésel, fabricada en Argentina) o, con esa salvedad, en el Corolla 1.8.
La guía de MANN Australia es del mismo tipo: se usaron solo las generaciones que se venden en
Argentina (Hilux GUN126 con motor 1GD-FTV, Ranger PX y PY, Amarok de primera generación 2H; no la
Amarok T1 de 2022, que en Argentina no se vende). El folleto de aplicaciones de MANN Brasil (ML) es del mismo tipo que el catálogo de Bosch: da el
filtro por versión de motor y período. Se usó para sumar el número de MANN a filtros que ya tenían
fuente, porque es el que publica la casa de repuestos que da los precios. Para el Sandero 1.6 el
folleto lista los motores K7M (8V) y K4M (16V); el Logan 1.6 usa los mismos, y Bosch (BF) da la
misma pieza para los dos. Donde otra fuente cubre el mismo vehículo, coincide
(por ejemplo, el filtro de aire de la Hilux en MI y MC, y el de aceite de la Ranger 2.0 en MH).

## Repuestos

| Código interno | Pieza | Equivalencias | Aplica a | Fuente |
|---|---|---|---|---|
| `PAST-VW-GOL-DEL` | Pastillas de freno delanteras | 5U0-698-151-A · JZZ-698-151-AF · Cobreq N-288 | Gol 2009–2019 | VW p. 7 |
| `DISC-VW-GOL-DEL` | Discos de freno delanteros (par) | 5U0-615-301-C · JZZ-698-302-C · Fremax BD-5297 | Gol 2009–2019 | VW p. 4 |
| `ZAPA-VW-GOL-TRAS` | Zapatas de freno traseras | 5U0-698-525-B · JZZ-698-520 · Bosch 0 986 487 270 | Gol 2009–2016 | VW p. 9 |
| `FILT-AIRE-VW-16` | Filtro de aire del motor | 5Z0-129-620 · JZZ-129-620-C · Fram CA9410 · Tecfil ART6098 · Bosch 0 986 B02 371 · MANN C 2295/2 | Gol 1.6 2009–2019 | VW p. 12; BF p. B62; ML p. 7 |
| `FILT-ACE-VW-16` | Filtro de aceite | 030-115-561-AR · 030-115-561-AQ · JZZ-115-561-A · Fram PH 10907 · Tecfil PSL560 · Bosch 0 986 B00 016 · Bosch 0 986 B00 043 · MANN W 712/53 | Gol 1.6 2009–2019 | VW p. 13; BF p. B62; ML p. 7 |
| `BUJI-VW-GOL-16` | Juego de bujías | Bosch F 000 KE0 P30 (FR 5 DE 2) · SP 30 | Gol 1.6 8V (CCRA) 2009–2019 | BV p. B184 |
| `BOBI-VW-GOL-16` | Bobina de encendido | Bosch F 000 ZS0 210 | Gol 1.6 8V (CCRA) 2009–2016 | BV p. B184 |
| `CABL-VW-GOL-16` | Juego de cables de bujía | Bosch F 000 99C 125 | Gol 1.6 8V (CCRA) 2009–2016 | BV p. B184 |
| `FILT-COMB-VW-REN-16` | Filtro de combustible | 6QE-201-511-C · JZW-201-511 · Bosch 0 986 BF0 024 · MANN WK 58/3 · Tecfil GI50/7 | Gol 1.6 2009–2016; Sandero 1.6 2011–2015; Logan 1.6 2011–2013 | BF pp. B62, B52, B53; VW p. 10 (indica también Logan y Sandero); ML pp. 6, 7 |
| `KIT-EMB-VW-GOL` | Kit de embrague (placa y disco) | JZZ-198-015-C · 036-141-026-Q + 032-141-033-D · 030-141-026-F + 030-141-034-L | Gol 2009–2019, caja manual | VW p. 21 |
| `BATE-12V-50AH` | Batería 12V 50Ah (tamaño H4) | 5GM-915-105-AF · -AG · -B · -C · JZZ-915-105 | Gol 2013–2019; Onix 2013 en adelante; 208 2013 en adelante; Sandero 2011 en adelante | VW p. 24 (VW indica Onix, 208 y Sandero en "Outras marcas") |
| `FILT-AIRE-GM-ONIX-14` | Filtro de aire del motor | Bosch 0 986 B02 530 · MANN C 24 034 | Onix 1.4 (X14YFH) 2013–2019 | BF p. B12; ML p. 5 |
| `FILT-ACE-GM-ONIX-14` | Filtro de aceite | Bosch 0 986 B00 041 · Bosch 0 986 B00 021 · MANN W 712/22 | Onix 1.4 (X14YFH) 2013–2019 | BF p. B12; ML p. 5 |
| `FILT-COMB-GM-ONIX-14` | Filtro de combustible | Bosch 0 986 BF0 018 · MANN WK 58 | Onix 1.4 (X14YFH) 2013–2019 | BF p. B12; ML p. 5 |
| `BUJI-GM-ONIX-14` | Juego de bujías | Bosch F 000 KE0 P34 (WR 6 DC+) · SP 34 | Onix 1.4 (X14YFH) 2013–2019 | BV p. B59 |
| `CABL-GM-ONIX-14` | Juego de cables de bujía | Bosch F 000 99C 612 | Onix 1.4 (X14YFH) 2013–2019 | BV p. B59 |
| `FILT-AIRE-PSA-208-16` | Filtro de aire del motor | Bosch 0 986 B02 367 | 208 1.6 16V (NFP) 2013–2020 | BF p. B48 |
| `FILT-ACE-PSA-208-16` | Filtro de aceite | Bosch 1 457 429 249 | 208 1.6 16V (NFP) 2013–2020 | BF p. B48 |
| `BUJI-PSA-208-16` | Juego de bujías | Bosch F 000 KE0 P51 (FR 5 ME) | 208 1.6 16V (NFP) 2013–2020 | BV p. B145 |
| `BOBI-PSA-208-16` | Bobina de encendido | Bosch F 000 ZS1 426 | 208 1.6 16V (NFP) 2015–2020 | BV p. B145 |
| `FILT-ACE-REN-16` | Filtro de aceite | Bosch 0 451 103 332 · MANN W 75/2 | Sandero 1.6 2011–2015; Logan 1.6 2011–2013 | BF pp. B52, B53; ML p. 6 |
| `FILT-AIRE-REN-16-8V` | Filtro de aire del motor (1.6 8V) | Bosch 0 986 B02 372 · MANN C 2680 | Sandero 1.6 8V (K7M) 2011–2015; Logan 1.6 8V 2011–2012 | BF pp. B52, B53; ML p. 6 |
| `FILT-AIRE-REN-16-16V` | Filtro de aire del motor (1.6 16V) | Bosch 0 986 B02 333 · MANN C 1858/4 | Sandero 1.6 16V (K4M) 2011–2014; Logan 1.6 16V 2011–2013 | BF pp. B52, B53; ML p. 6 |
| `BUJI-REN-16-8V` | Juego de bujías (1.6 8V) | Bosch F 000 KE0 P20 (FR 7 LD+) · SP 20 | Sandero y Logan 1.6 8V (K7M) 2011–2015 | BV pp. B156, B157 |
| `BUJI-REN-16-16V` | Juego de bujías (1.6 16V) | Bosch F 000 KE0 P49 (FQR 6 DCW+) | Sandero 1.6 16V (K4M) 2011–2014; Logan 2011–2013 | BV pp. B156, B157 |
| `BOBI-REN-16-8V` | Bobina de encendido (1.6 8V) | Bosch 0 986 221 060 | Sandero y Logan 1.6 8V (K7M) 2011–2015 | BV pp. B156, B157 |
| `CABL-REN-16-8V` | Juego de cables de bujía (1.6 8V) | Bosch F 000 99C 135 | Sandero y Logan 1.6 8V (K7M) 2011–2015 | BV pp. B156, B157 |
| `BUJI-FIAT-CRONOS-18` | Juego de bujías | Bosch 0 242 135 563 (YR 7 KII 33 T) | Cronos 1.8 16V (E.torQ) 2018 en adelante | BV p. B74 |
| `FILT-AIRE-TOY-COR-18` | Filtro de aire del motor | MAHLE LX 7292 · MANN C 24 005 | Corolla 1.8 2014–2019 | MH p. 57; ML p. 7 |
| `FILT-ACE-TOY-COR-18` | Filtro de aceite (cartucho) | MAHLE OX 416D1 · MANN HU 6006 x | Corolla 1.8 2014–2016 | MH p. 57; ML p. 7 |
| `FILT-ACE-TOY-COR-18-R` | Filtro de aceite (roscado) | MAHLE OC 340 | Corolla 1.8 2017–2019 | MH p. 57 |
| `FILT-ACE-FORD-RAN-TDCI` | Filtro de aceite (cartucho) | MAHLE OX 834D · MANN HU 7002 z | Ranger 2.2 y 3.2 TDCi 2012–2022; 2.0 Bi-Turbo 2023 en adelante | MH pp. 29–30; MU p. 4 |
| `FILT-AIRE-FORD-RAN-32` | Filtro de aire del motor | MANN C 22 024 | Ranger 3.2 TDCi 2012–2022 | MU p. 4 |
| `FILT-COMB-FORD-RAN-32` | Filtro de combustible | MANN PU 9008 z | Ranger 3.2 TDCi 2012–2022 | MU p. 4 |
| `FILT-AIRE-FORD-RAN-23` | Filtro de aire del motor | MANN C 34 001 | Ranger 2.0 Bi-Turbo y 3.0 V6, 2023 en adelante | MU p. 4 |
| `FILT-ACE-FORD-RAN-V6` | Filtro de aceite (cartucho) | MANN HU 826 x | Ranger 3.0 V6, 2023 en adelante | MU p. 4 |
| `FILT-COMB-FORD-RAN-23` | Filtro de combustible | MANN WK 11 027 | Ranger 2.0 Bi-Turbo y 3.0 V6, 2023 en adelante | MU p. 4 |
| `PAST-VW-AMAROK-DEL` | Pastillas de freno delanteras | 2H0-698-151-A · JZZ-698-151-AH · Cobreq N-293 | Amarok 2010 en adelante | VW p. 7 |
| `FILT-AIRE-VW-AMAROK` | Filtro de aire del motor | MANN C 32 013 · Bosch F 026 400 286 · MAHLE LX 3138 · MAHLE LX 3139 | Amarok 2.0 TDI 2010 en adelante; 3.0 V6 TDI 2020 en adelante | MU p. 5; BF p. B58; MH p. 58 |
| `FILT-ACE-VW-AMAROK-20` | Filtro de aceite (cartucho) | MANN HU 7008 z · Bosch F 026 407 023 · MAHLE OX 388D | Amarok 2.0 TDI 2010 en adelante | MU p. 5; BF p. B58; MH p. 58 |
| `FILT-ACE-VW-AMAROK-V6` | Filtro de aceite (cartucho) | MANN HU 7012 z | Amarok 3.0 V6 TDI 2020 en adelante | MU p. 5 |
| `FILT-COMB-VW-AMAROK-20` | Filtro de combustible | MANN WK 9016 · Bosch F 026 402 828 · MAHLE KL 787 | Amarok 2.0 TDI 2010 en adelante | MU p. 5; BF p. B58; MH p. 58 |
| `FILT-COMB-VW-AMAROK-V6` | Filtro de combustible | MANN WK 10 046 z | Amarok 3.0 V6 TDI 2020 en adelante | MU p. 5 |
| `FILT-AIRE-TOY-HILUX` | Filtro de aire del motor | 17801-0L040 · MANN C 33 017/1 · MANN C 33 017 · Tecfil ARL2206 | Hilux 2.4 y 2.8 diésel 2016 en adelante | MI (11/2021); MC; MU p. 5 |
| `FILT-ACE-TOY-HILUX-28` | Filtro de aceite | MANN W 712/83 | Hilux 2.8 diésel (1GD-FTV) 2016 en adelante | MU p. 5 |
| `FILT-COMB-TOY-HILUX-28` | Filtro de combustible | MANN PU 9023 z | Hilux 2.8 diésel (1GD-FTV) 2016 en adelante | MU p. 5 |
| `FILT-AIRE-REN-SCE` | Filtro de aire del motor | MANN C 27 030/1 · MANN C 27 030 | Sandero 1.6 16V SCe 2016 en adelante | MI (10/2021) |

Los años de cada aplicación son la intersección entre el período que publica la fuente y la
configuración del vehículo en el catálogo (por ejemplo, Bosch publica el Gol VI 1.6 hasta 03/2016:
la pieza aplica hasta 2016).

## Decisiones y límites

- **El código interno no es un número de fabricante.** Es el identificador de RepMatch. Los números
  reales están en las equivalencias, que también se usan para buscar ofertas en las tiendas.
- **Una configuración puede tener dos motores.** El catálogo de vehículos no distingue, por ejemplo,
  el Sandero 1.6 8V (K7M) del 1.6 16V (K4M). Cuando la pieza cambia con el motor, hay dos piezas y el
  nombre y la descripción dicen a cuál corresponde; las que sirven para los dos (aceite, combustible)
  son una sola.
- **Variantes que la fuente marca.** Volkswagen lista para el Gol, desde 2012, otras pastillas
  (5U0-698-151-B) y desde 2013 otro disco (1S0-615-301-F), según el sistema de freno. Se cargó la
  referencia que cubre todo el período y la descripción avisa de la variante.
- **Lo que no tiene fuente quedó afuera.** No se encontró un catálogo descargable de pastillas,
  amortiguadores o correas para las marcas que no son Volkswagen (Fras-le, Cobreq y Bosch solo
  ofrecen buscadores en línea). Tampoco hay datos para Cronos 1.3, Corolla 2.0 / híbrido ni el
  Logan SCe; de la Hilux 2.4 solo está el filtro de aire. Esos vehículos se sacaron del catálogo de vehículos: un modelo
  sin ninguna pieza sería una búsqueda que siempre vuelve vacía.
- **Los códigos que tenía el catálogo anterior no se reutilizaron.** No figuraban en ninguna fuente
  o figuraban con otro sufijo (por ejemplo, la pastilla del Gol es 5U0-698-151-A, no 5U0698151).

## Cómo sumar repuestos

1. Ubicar la pieza en un catálogo oficial (automotriz o fabricante) que se pueda citar.
2. Agregarla en `DatosSemilla.ConstruirCatalogo` con sus equivalencias y aplicaciones.
3. Agregar la fila en este documento con la fuente y la página.
4. Si es un vehículo nuevo, sumar su configuración en `ConstruirConfiguracionesVehiculo` después
   de tener al menos una pieza: la prueba
   `Cada_modelo_del_catalogo_tiene_al_menos_un_repuesto_compatible` lo exige.

El catálogo se carga solo en una base vacía (`DatosSemilla.SembrarAsync`): para verlo en un entorno
existente hay que recrear la base (`docker compose down -v`).
