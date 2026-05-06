# ProfileHeightService (Level 2)

## Назначение
Сервис для сохранения и загрузки данных о видимой высоте профилей с метаданными. Обеспечивает сохранение значений по умолчанию, отслеживание ручных изменений и цветовую индикацию в пользовательском интерфейсе.

## Зависимости
- `LinearCutWpf.Models` - для работы с моделями данных
- `System.Xml.Linq` - для работы с XML-файлами
- `System.IO` - для работы с файловой системой

## Основные методы

### SaveProfileHeightsWithMetadata
```csharp
public static void SaveProfileHeightsWithMetadata(IEnumerable<ArticleGroupingRow> profileRows, double? defaultHeight)
```
Сохраняет данные о высоте профилей и длине хлыста в XML-файл с метаданными.

**Параметры:**
- `profileRows`: Коллекция строк с данными о высотах профилей и длинах хлыстов
- `defaultHeight`: Высота по умолчанию

**Особенности:**
- Сохраняет артикул, высоту, длину хлыста и флаги метаданных
- Сохраняет высоту по умолчанию в корневом элементе
- Использует XML-формат для хранения данных
- Сохраняет артикулы с непустой высотой, длиной хлыста или флагами изменений

### LoadProfileHeightsWithMetadata
```csharp
public static Dictionary<string, (double? height, bool isDefaultValue, bool isManuallyChanged, double? barLength, bool isBarLengthManuallyChanged)> LoadProfileHeightsWithMetadata()
```
Загружает данные о высоте профилей и длине хлыста из XML-файла с метаданными.

**Возвращает:**
Словарь, где ключ - артикул, значение - кортеж (высота, является значением по умолчанию, изменено вручную, длина хлыста, длина хлыста изменена вручную)

### LoadDefaultHeight
```csharp
public static double? LoadDefaultHeight()
```
Загружает высоту по умолчанию из файла.

**Возвращает:**
Высота по умолчанию или null, если не задана

## Структура XML-файла
```xml
<ProfileHeights DefaultHeight="1000">
  <Profiles>
    <Profile Article="ART001" VisibleHeight="1200" IsDefaultValue="False" IsManuallyChanged="True" BarLength="6000" IsBarLengthManuallyChanged="True"/>
    <Profile Article="ART002" VisibleHeight="1000" IsDefaultValue="True" IsManuallyChanged="False" BarLength="null" IsBarLengthManuallyChanged="False"/>
  </Profiles>
</ProfileHeights>
```

## Логика работы
1. **Значения по умолчанию**: Записываются только в пустые ячейки
2. **Отслеживание изменений**: При ручном изменении ячейки устанавливается флаг `IsManuallyChanged` (для высоты) или `IsBarLengthManuallyChanged` (для длины хлыста)
3. **Обновление значений**: При изменении высоты по умолчанию автоматически обновляются только ячейки со значением по умолчанию, которые не были изменены вручную
4. **Длина хлыста**: Сохраняется в атрибуте `BarLength` (значение "null" означает отсутствие пользовательского выбора)

## Интеграция с UI
- Используется `ArticleSettingsControl` для загрузки/сохранения данных
- Цветовая индикация через `DataTrigger` в XAML:
  - Светло-голубой (`#E6F3FF`): высота — значение по умолчанию
  - Розовый (`#FFB6C1`): высота или длина хлыста — изменено вручную (отличается от значения по умолчанию)
  - Серый (`#E6E6E6`): длина хлыста/пресет — значение по умолчанию (не выбрано)
