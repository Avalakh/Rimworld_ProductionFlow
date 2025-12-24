# ProductionFlow Mod для RimWorld 1.6

Мод добавляет новую вкладку на нижней панели для управления производственными процессами колонии.

## Функции

- ✅ Новая вкладка "ProductionFlow" на нижней панели
- ✅ Отображение всех производящих единиц (верстаков) колонии
- ✅ Выбор продукта для производства
- ✅ Фильтрация верстаков по выбранному продукту (показываются только те, которые могут произвести выбранный продукт)
- ✅ Выбор количества для производства
- ✅ Выбор качества продукции
- ✅ Создание задач (Bill) на выбранные верстаки

## Требования

- RimWorld 1.6

## Установка

1. Скопируйте папку `ProductionFlow` в папку `Mods` RimWorld
2. Включите мод в меню модов
3. Запустите игру

## Компиляция

Для компиляции мода используйте PowerShell:

```powershell
cd "D:\Games\_Install\Rimworld\Mods\ProductionFlow"
.\build.ps1 -RimWorldPath "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
```

Или используйте batch файл:

```cmd
cd "D:\Games\_Install\Rimworld\Mods\ProductionFlow"
compile.bat "C:\Program Files (x86)\Steam\steamapps\common\RimWorld"
```

После компиляции DLL файл будет создан в папке `Assemblies\ProductionFlow.dll`

## Использование

1. Откройте вкладку "ProductionFlow" на нижней панели
2. Выберите продукт из списка доступных продуктов
3. Введите количество для производства
4. При необходимости выберите качество продукции
5. Выберите верстаки из списка (они автоматически фильтруются по выбранному продукту)
6. Нажмите "Create Bills" для создания задач на выбранные верстаки

## Структура мода

```
ProductionFlow/
├── About/
│   ├── About.xml
│   └── Manifest.xml
├── Assemblies/
│   └── ProductionFlow.dll (после компиляции)
├── Defs/
│   └── MainTabDefs/
│       └── ProductionFlowTab.xml
├── Languages/
│   └── English/
│       └── Keyed/
│           └── ProductionFlow.xml
├── Source/
│   └── ProductionFlow/
│       ├── MainTabWindow_ProductionFlow.cs
│       ├── ProductionFlowMod.cs
│       ├── ProductionFlow.csproj
│       └── Properties/
│           └── AssemblyInfo.cs
├── build.ps1
├── compile.bat
└── README.md
```

## Технические детали

Мод использует стандартный API RimWorld для:
- Получения всех верстаков колонии через `IBillGiver` интерфейс
- Получения рецептов через `ThingDef.AllRecipes`
- Создания задач через `Bill_Production` и `BillStack.AddBill`
- Создания UI через `MainTabWindow`

## Примечания

- Мод работает только с верстаками, которые реализуют интерфейс `IBillGiver`
- Качество продукции можно выбрать только для рецептов, которые требуют навык работы (workSkill)
- При выборе продукта список верстаков автоматически обновляется, показывая только те, которые могут произвести выбранный продукт

