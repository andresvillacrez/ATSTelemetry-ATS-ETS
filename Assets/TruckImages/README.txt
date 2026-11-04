IMAGENES DE CAMION
==================

Esta carpeta contiene una imagen por modelo oficial de camion para el
hueco inferior del panel DANOS. La app la dibuja centrada, escalada de
forma proporcional, con margen y esquinas redondeadas. Al cambiar de
modelo, la imagen nueva se funde con la anterior durante 0,5 segundos;
la primera carga y el aviso de modelo sin imagen aparecen de inmediato.

COBERTURA
---------

45 modelos oficiales instalados:

  - 19 de American Truck Simulator (ATS)
  - 26 de Euro Truck Simulator 2 (ETS2)

No se incluyen mods. La imagen es por modelo, no por pintura ni
configuracion del jugador.

FORMATO
-------

- JPG de 1200 x 600 (proporcion 2:1), calidad 90.
- Imagen del concesionario o render de estudio, sin texto superpuesto ni
  controles de la interfaz.
- Fondo de estudio o showroom; se muestra dentro de un recorte redondeado.

NOMBRE DEL ARCHIVO
------------------

El nombre debe coincidir con el TruckId del juego sin el prefijo
"vehicle." y con los puntos convertidos en guiones bajos:

  vehicle.kenworth.w900  ->  kenworth_w900.jpg
  vehicle.peterbilt.389  ->  peterbilt_389.jpg

La app tambien prueba sin sufijos de mod (_gtm, _mod, _custom) y, como
respaldo, la combinacion TruckBrand + TruckName.

ATS (19)
--------

  freightliner_cascadia2019.jpg  Freightliner Cascadia (2019)
  freightliner_cascadia2024.jpg  Freightliner Cascadia (2024)
  intnational_9900i.jpg          International 9900i
  intnational_lonestar.jpg       International LoneStar
  intnational_lt.jpg             International LT
  kenworth_t680.jpg              Kenworth T680
  kenworth_t680_2022.jpg         Kenworth T680 (2022)
  kenworth_w900.jpg              Kenworth W900
  mack_anthem.jpg                Mack Anthem
  mack_pinnacle.jpg              Mack Pinnacle
  peterbilt_389.jpg              Peterbilt 389
  peterbilt_579.jpg              Peterbilt 579
  volvo_vnl.jpg                  Volvo VNL
  volvo_vnl2018.jpg              Volvo VNL (2018)
  volvo_vnl2025.jpg              Volvo VNL (2025)
  volvo_vnr_e.jpg                Volvo VNR Electric
  westernstar_49x.jpg            Western Star 49X
  westernstar_5700xe.jpg         Western Star 5700XE
  westernstar_57x.jpg            Western Star 57X

ETS2 (26)
---------

  daf_2021.jpg                   DAF 2021
  daf_xd.jpg                     DAF XD
  daf_xf.jpg                     DAF XF
  daf_xf_electric.jpg            DAF XF Electric
  daf_xf_euro6.jpg               DAF XF Euro 6
  iveco_hiway.jpg                Iveco Hi-Way
  iveco_stralis.jpg              Iveco Stralis
  iveco_sway.jpg                 Iveco S-Way
  man_tgx.jpg                    MAN TGX
  man_tgx_2020.jpg               MAN TGX (2020)
  man_tgx_euro6.jpg              MAN TGX Euro 6
  mercedes_actros.jpg            Mercedes-Benz Actros
  mercedes_actros2014.jpg        Mercedes-Benz Actros (2014)
  renault_etech_t.jpg            Renault E-Tech T
  renault_magnum.jpg             Renault Magnum
  renault_premium.jpg            Renault Premium
  renault_t.jpg                  Renault T
  scania_r.jpg                   Scania R
  scania_r_2016.jpg              Scania R (2016)
  scania_s_2016.jpg              Scania S (2016)
  scania_s_2024e.jpg             Scania S (2024)
  scania_streamline.jpg          Scania Streamline
  volvo_fh_2021.jpg              Volvo FH (2021)
  volvo_fh_2024.jpg              Volvo FH (2024)
  volvo_fh16.jpg                 Volvo FH16
  volvo_fh16_2012.jpg            Volvo FH16 (2012)

PROCEDENCIA
-----------

- ATS: capturas de concesionario de la wiki de Truck Simulator
  (series ATS_*_Stock). Se recorto la zona del camion y el showroom y
  se ajusto el encuadre a 1200 x 600.
- ETS2: renders oficiales de la wiki de Truck Simulator sobre fondo de
  estudio neutro, ajustados a 1200 x 600.
- DAF XF Electric: imagen real publicada en el blog de SCS Software
  (DAF XF Electric Blog 1.jpg).
- Kenworth W900: se conserva el recorte original de
  ATS_Kenworth_Stock_06.png usado desde la v2.4.

No son dibujos generados ni el logotipo de la marca como sustituto. Si
un modelo no tiene imagen, el panel muestra "IMAGEN DEL MODELO NO
DISPONIBLE" y el resto de la telemetria sigue funcionando. Sin senal no
se dibuja nada.
