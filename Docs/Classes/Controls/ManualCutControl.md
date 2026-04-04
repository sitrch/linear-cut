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
