# Инструкция по замене MessageBox на кастомные диалоговые окна

## Созданные файлы

1. **Controls/CustomMessageBox.xaml** - XAML-разметка кастомного окна
2. **Controls/CustomMessageBox.xaml.cs** - код-библиотека окна
3. **Controls/CustomMessageBoxHelper.cs** - класс-помощник для легкой замены

## Как использовать

### Вариант 1: Быстрая замена через CustomMessageBoxHelper

Просто замените `MessageBox.Show` на `CustomMessageBoxHelper.Show`:

```csharp
// Было:
MessageBox.Show("Сообщение", "Заголовок", MessageBoxButton.OK, MessageBoxImage.Warning);

// Стало:
CustomMessageBoxHelper.Show("Сообщение", "Заголовок", MessageBoxButton.OK, MessageBoxImage.Warning);
```

Добавьте using в начало файла:
```csharp
using Diplom_Bibl.Controls;
```

### Вариант 2: Прямое использование CustomMessageBox

```csharp
var result = CustomMessageBox.Show(
    "Текст сообщения",
    "Заголовок",
    CustomMessageBoxButton.YesNo,
    CustomMessageBoxImage.Question
);

if (result == CustomMessageBoxResult.Yes)
{
    // Действие при нажатии "Да"
}
```

## Типы кнопок (CustomMessageBoxButton)

- `OK` - одна кнопка OK
- `OKCancel` - кнопки OK и Отмена
- `YesNo` - кнопки Да и Нет
- `YesNoCancel` - кнопки Да, Нет и Отмена
- `AbortRetryIgnore` - кнопки Прервать, Повторить, Игнорировать

## Типы иконок (CustomMessageBoxImage)

- `None` - без иконки
- `Information` - информация (синий круг с ℹ)
- `Warning` - предупреждение (желтый круг с ⚠)
- `Error` - ошибка (красный круг с ✕)
- `Question` - вопрос (синий круг с ?)

## Результаты (CustomMessageBoxResult)

- `OK`, `Cancel`, `Yes`, `No`, `Abort`, `Retry`, `Ignore`

## Стиль

Кастомное окно использует цвета из App.xaml:
- PrimaryColor (#0056B3) - для заголовка
- AccentColor (#007BFF) - для информационных иконок
- WarningColor (#FFC107) - для предупреждений
- DangerColor (#DC3545) - для ошибок
- LightGray (#E9ECEF) - для фона кнопок
- SaveButtonStyle - для кнопок

## Примеры замены

### Пример 1: Простое сообщение
```csharp
// Было:
MessageBox.Show("Запись сохранена", "Успешно");

// Стало:
CustomMessageBoxHelper.Show("Запись сохранена", "Успешно");
```

### Пример 2: Сообщение с иконкой
```csharp
// Было:
MessageBox.Show("Ошибка подключения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

// Стало:
CustomMessageBoxHelper.Show("Ошибка подключения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
```

### Пример 3: Диалог с выбором
```csharp
// Было:
var result = MessageBox.Show("Удалить запись?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
if (result == MessageBoxResult.Yes) { ... }

// Стало:
var result = CustomMessageBoxHelper.Show("Удалить запись?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
if (result == MessageBoxResult.Yes) { ... }
```

## Автоматическая замена во всем проекте

Можно использовать поиск и замену в IDE:
- Найти: `MessageBox\.Show\(`
- Заменить на: `CustomMessageBoxHelper.Show(`

Или через командную строку (PowerShell):
```powershell
Get-ChildItem -Recurse -Include *.cs | ForEach-Object {
    (Get-Content $_.FullName -Raw) -replace 'MessageBox\.Show\(', 'CustomMessageBoxHelper.Show(' | Set-Content $_.FullName
}
```

## Примечания

1. Кастомное окно поддерживает закрытие по ESC (возвращает Cancel)
2. Кнопка закрытия (✕) в заголовке также возвращает Cancel
3. Окно отображается по центру владельца (Owner)
4. Все стили соответствуют основному дизайну приложения
