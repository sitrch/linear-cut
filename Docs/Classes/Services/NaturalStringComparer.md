# NaturalStringComparer (Level 2)

## Назначение
Компаратор для естественной (смешанной числовой) сортировки строк. Обеспечивает сортировку, при которой числа внутри строк сравниваются как числа, а не лексикографически.

## Зависимости
- `System.Collections.Generic` (`IComparer<string>`)

## Основные методы

### Compare
```csharp
public int Compare(string x, string y)
```
Сравнивает две строки с использованием естественной сортировки.

**Логика:**
1. Строки разбиваются на сегменты: текстовые и числовые
2. Числовые сегменты сравниваются как целые числа
3. Текстовые сегменты сравниваются лексикографически (с учётом регистра по настройке)

**Пример:** "Арт1" < "Арт2" < "Арт10" (вместо лексикографического "Арт1" < "Арт10" < "Арт2")

### ParseNumber
```csharp
private static int ParseNumber(string s, ref int index)
```
Извлекает число из строки начиная с текущей позиции.

### CompareTextSegment
```csharp
private int CompareTextSegment(string x, ref int ix, string y, ref int iy)
```
Сравнивает текстовые сегменты двух строк начиная с текущих позиций.

## Свойства

### Instance
```csharp
public static readonly NaturalStringComparer Instance
```
Статический экземпляр компаратора (без учёта регистра) для повторного использования.

## Точки использования
- `DataStoreService.GetUniqueArticles()` — сортировка списка артикулов
- `ArticleSettingsControl.Initialize()` — сортировка групп при инициализации
- `MainWindow.RunOptimization()` — сортировка групп перед раскроем