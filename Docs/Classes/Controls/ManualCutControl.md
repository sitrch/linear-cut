# Описание класса ManualCutControl

## Уровень: Local (Level 2)
**Пространство имен:** `LinearCutWpf.Controls`  
**Сборка:** `LinearCutWpf`  
**Тип:** `UserControl`  

## Назначение
Контрол `ManualCutControl` предоставляет пользовательский интерфейс для ручного управления процессом раскроя. Он связывает UI с `ManualCutViewModel`, обеспечивая взаимодействие пользователя с логикой ручного раскроя.

## Зависимости (DI и связи)
- **ViewModel:** Инициализирует и использует `ManualCutViewModel` в качестве контекста данных (`DataContext`).

## Основные элементы и методы
- **Свойство `ViewModel`** (`ManualCutViewModel`): Доступ к модели представления для ручного раскроя.
- **Конструктор `ManualCutControl()`**: Инициализирует компоненты UI, создает экземпляр `ManualCutViewModel` и устанавливает его как `DataContext`.

## UI-элементы DataGrid
- **Ширина колонок**: Пропорциональные (`*`) — колонки растягиваются на всю доступную ширину. Хлыст/Размеры/Исп.остатки = `2*`, Кол-во = `1*`, Удалить = `Auto`.
- **Контекстное меню заголовка**: Пункт "Удалить" с привязкой к `DeleteRowCommand` через `PlacementTarget.Tag` (DataGrid сохраняется в `Tag` заголовка через `RelativeSource AncestorType=DataGrid`). Удаляет выделенную строку (`SelectedItem`).
- **Контекстное меню строки**: Пункт "Удалить строку" с привязкой к `DeleteRowCommand` через `PlacementTarget.Tag` (ViewModel сохраняется в `Tag` строки через `ElementName`-привязку к `DataContext` DataGrid).
- **Колонка "Удалить"**: Кнопка "✕" в каждой строке, привязанная к `DeleteRowCommand` с текущей строкой как `CommandParameter`.
