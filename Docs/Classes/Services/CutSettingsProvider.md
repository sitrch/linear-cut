# CutSettingsProvider

**Namespace:** `LinearCutWpf.Services`
**Тип:** `static class`

## Назначение
Класс отвечает за сериализацию и десериализацию пользовательских настроек приложения в XML-файл (`settings.xml`). Файл сохраняется в директории приложения (`AppDomain.CurrentDomain.BaseDirectory`).

Класс обеспечивает сохранение состояния интерфейса (размеры окна, ширина левой панели) и рабочих параметров (пресеты реза, доступные длины хлыстов), что позволяет сохранять пользовательский опыт между перезапусками приложения.

## Зависимости
- `System.Xml.Linq` (использует `XDocument`, `XElement`, `XAttribute` для работы с XML).
- `LinearCutWpf.Models` (оперирует моделями `PresetModel` и `StockLengthModel`).

## Основные методы

### Работа с пресетами
- **`LoadAll()`**: Читает список `PresetModel` (элементы `<Preset>`). Если файла нет, возвращает пресет по умолчанию (торцовка: 10/10, ширина реза: 4).
- **`SaveAll(List<PresetModel> presets)`**: Перезаписывает узел `<Presets>` в `settings.xml`.
- **`LoadDefaultPresetName()`** / **`SaveDefaultPresetName(string presetName)`**: Управление именем пресета, выбранного по умолчанию.

### Работа с длинами хлыстов
- **`LoadStockLengths()`**: Читает список `StockLengthModel`. Значение по умолчанию: одна длина (6000 мм, активна).
- **`SaveStockLengths(List<StockLengthModel> stockLengths)`**: Сохраняет коллекцию доступных хлыстов в узел `<StockLengths>`.
- **`LoadDefaultStockLength()`** / **`SaveDefaultStockLength(double length)`**: Управление длиной хлыста по умолчанию, выбранной в интерфейсе.

### Настройки UI
- **`LoadLeftPanelWidth()`** / **`SaveLeftPanelWidth(double width)`**: Сохраняет ширину разделителя (`GridSplitter`) главного окна (по умолчанию 450).
- **`LoadWindowSettings()`** / **`SaveWindowSettings(WindowSettings settings)`**: Сохранение координат (`Left`, `Top`), размеров (`Width`, `Height`) и состояния окна (`WindowState`) для восстановления окна при следующем запуске. Вложенный класс `WindowSettings` используется как контейнер этих данных.

## Особенности реализации
- При каждом сохранении (`Save*`) файл `settings.xml` загружается целиком (`XDocument.Load`), нужный узел удаляется и создается заново с новыми данными, затем файл сохраняется.
- Если файла нет, `Save*` методы создают новый документ с корневым узлом `<Settings>`.
- Безопасный парсинг с использованием `.TryParse` или блоков fallback-значений (`?? "0"`) предотвращает падения приложения при ручном редактировании или повреждении XML-файла.
