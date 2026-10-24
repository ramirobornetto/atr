# Modificar la apariencia de ATR

Abrí `WpfPrototype/Appearance.xaml` en Visual Studio. Los valores del comienzo son el panel de ajustes visuales. Guardá con Ctrl+S y ejecutá con F5. Muchos cambios también se reflejan con XAML Hot Reload mientras la app está abierta; si no, cerrala y ejecutala otra vez.

## Valores comunes

| Nombre | Qué modifica | Valor inicial |
| --- | --- | --- |
| ControlHeight | Alto de botones, campos de una línea y desplegables cerrados | 34 |
| PlanNumberHeight | Alto de los campos numéricos de tiempos en Plan | 24 |
| PlanDropdownHeight | Alto de los selectores de origen e historial en Plan | 28 |
| LibrarySearchHeight | Alto del campo de búsqueda en Biblioteca | 30 |
| TableControlHeight | Alto de los desplegables dentro de correspondencias | 23 |
| TableRowHeight | Alto de las filas de las tablas | 29 |
| TableHeaderHeight | Alto de los encabezados de las tablas | 38 |
| BodyFontSize | Tamaño de letra general; los títulos tienen ajustes propios | 12 |
| BodyFontFamily | Tipo de letra general | Segoe UI |
| ButtonPadding | Espacio interior de los botones | 12,5,12,5 |
| InputPadding | Espacio interior de los campos de texto | 9,4,9,4 |
| ControlMargin | Separación exterior habitual de los botones | 3,2,3,2 |
| SourcePathMargin | Separación alrededor de la ruta del historial en Plan | 0,8,0,14 |

Las medidas son unidades WPF: equivalen a píxeles con Windows al 100 %. Para separaciones, cuatro números significan izquierda, arriba, derecha y abajo. Usá punto para decimales, por ejemplo `32.5`.

Debajo están los colores: `Bg` (fondo), `Panel`, `Inset`, `Accent` (acento), `Muted` (texto secundario), `ButtonBackground`, `ButtonForeground`, `ControlBorder`, `InputBackground` e `InputForeground`. Modificá el valor `Color`, conservando el formato `#RRGGBB`. No cambies los nombres `x:Key`.

La navegación lateral, los títulos, avisos, registros de varias líneas y algunos estados tienen medidas o colores específicos. No todos los espacios de cada pantalla usan el mismo margen. Los controles de tabla tienen su propia altura para no agrandar todas las filas. Si aumentás su tamaño de letra o altura, aumentá también la altura de la fila.

## Cambiar solamente un elemento

Para cambiar solo «Goldberg», abrí `MainWindow.xaml`, seleccioná ese botón en el diseñador (su nombre es `ImportGoldbergButton`), pulsá F4 y poné `40` en `Height`. Solamente ese botón cambiará. También podés escribir la propiedad en su etiqueta:

```xml
<Button x:Name="ImportGoldbergButton"
        Content="Goldberg"
        Click="ImportGoldbergClick"
        Margin="0,3,0,7"
        Style="{StaticResource CompactButton}"
        Height="40" />
```

Para volver al valor común, borrá el atributo `Height="40"`, o restablecé esa propiedad en el panel de Visual Studio. No borres `x:Name` ni `Click`: conectan el control con su función.

El mismo criterio funciona para `Width` (ancho), `Background` (fondo), `Foreground` (texto), `FontSize`, `Margin` y `Padding`: el valor del elemento tiene prioridad sobre el estilo compartido. Cambiar `Height` a un valor menor también está permitido; comprobá que el texto y el espacio interior entren.

## Dónde se edita cada cosa

- `Appearance.xaml`: valores comunes y estilos. Empezá por la sección comentada del comienzo; la parte inferior contiene las plantillas de los controles.
- `MainWindow.xaml`: ubicación, columnas, textos y propiedades individuales de las pestañas principales.
- `PlatformMappingWindow.cs`: distribución de correspondencias. Sus controles usan los estilos comunes, pero su ubicación todavía se construye desde código.
- `App.xaml`: carga el archivo de apariencia; normalmente no necesitás modificarlo.

Hacé un cambio a la vez y conservá una copia del proyecto. Este archivo queda incorporado al compilar: editarlo junto a un ejecutable ya publicado no cambia ese ejecutable. Para probar cambios usá el proyecto abierto en Visual Studio.


## Apariencia neón (0.17.0)

El fondo está incorporado como `WpfPrototype/Assets/atr-background.png`, con desenfoque suave ya aplicado. Se adapta a la ventana conservando las proporciones y recortando los bordes si hace falta. `BackgroundShade` controla la capa oscura sobre la imagen.

Los colores con ocho dígitos usan `#AARRGGBB`: el primer par indica opacidad (`00` invisible, `FF` opaco). `Panel` e `Inset` usan este formato para que el paisaje se vea a través de sus fondos. No se aplica opacidad a todo el panel, de modo que el texto sigue siendo sólido.

Las plantillas de `Button`, `DangerButton`, `NavButton`, `ComboBox`, `ComboBoxItem` y `CheckBox` están en `Appearance.xaml`. Los efectos y gradientes se editan allí. Las letras e iconos de la navegación tienen tamaños específicos (14 y 20), independientes del resto de botones. La navegación activa utiliza `Selector.IsSelected`; no se debe reemplazar su estado por un cambio manual de fondo desde el código.


Desde 0.17.1, `Panel`, `Inset` y `BackgroundShade` usan negro neutro. El estilo `DataGrid` define las dos tonalidades translúcidas de las filas y elimina sus líneas; `DataGridCell` centra verticalmente su contenido. El contorno completo de navegación se define en `NavOutline` y usa grosor uniforme de 1,5.


Desde 0.17.2, `Panel` usa `#99080808`: 60 % opaco y 40 % transparente. `Inset` conserva su transparencia anterior. El estilo `GridSplitter` dibuja una línea de 1 unidad WPF sobre la zona de agarre transparente de 6; su brillo está definido en `SplitterLine`.

Desde 0.17.3 los separadores son completamente transparentes: se mantiene una zona de agarre de 6 unidades, sin línea ni glow. El cursor indica dónde se puede ajustar la tabla.

Desde 0.17.4, `Panel` usa `#73080808` (aproximadamente 55 % transparente) e `Inset` usa `#8C040404` (aproximadamente 45 % transparente). Los paneles anidados superponen sus fondos.

Desde 0.17.6, los paneles no tienen borde gris. `BackgroundShade` usa `#86000000`, reduciendo aproximadamente un 10 % la contribución de la imagen respecto del valor anterior `#78000000`. Las transparencias de `Panel` e `Inset` se conservan.

Desde 0.17.7, el cuadro `LogBox` usa `#660B1927`: azul oscuro con 40 % de opacidad y 60 % de transparencia.
