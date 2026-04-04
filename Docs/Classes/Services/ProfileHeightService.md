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
Сохраняет данные о высоте профилей в XML-файл с метаданными.

**Параметры:**
- `profileRows`: Коллекция строк с данными о высотах профилей
- `defaultHeight`: Высота по умолчанию

**Особенности:**
- Сохраняет только основные данные: артикул и высоту
- Сохраняет высоту по умолчанию в корневом элементе
- Использует упрощенный XML-формат для хранения данных
- Не сохраняет артикулы с пустой высотой профиля

### LoadProfileHeightsWithMetadata
```csharp
public static Dictionary<string, (double? height, bool isDefaultValue, bool isManuallyChanged)> LoadProfileHeightsWithMetadata()
```
Загружает данные о высоте профилей из XML-файла с метаданными.

**Возвращает:**
Словарь, где ключ - артикул, значение - кортеж (высота, является значением по умолчанию, изменено вручную)

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
    <Profile Article="ART001" VisibleHeight="1200" IsDefaultValue="False" IsManuallyChanged="True"/>
    <Profile Article="ART002" VisibleHeight="1000" IsDefaultValue="True" IsManuallyChanged="False"/>
  </Profiles>
</ProfileHeights>
```

## Логика работы
1. **Значения по умолчанию**: Записываются только в пустые ячейки
2. **Отслеживание изменений**: При ручном изменении ячейки устанавливается флаг `IsManuallyChanged`
3. **Обновление значений**: При изменении высоты по умолчанию автоматически обновляются только ячейки со значением по умолчанию, которые не были изменены вручную

## Интеграция с UI
- Используется `ArticleSettingsControl` для загрузки/сохранения данных
- Цветовая индикация через `HeightValueColorConverter`:
  - Светло-голубой: значения по умолчанию
  - Светло-золотой: измененные вручную значения