# Примеры отчетов автоматического рецензирования Merge Request

В этом документе представлены образцы технических отчетов, генерируемых навыком `kbpro-mr-reviewer` с использованием цветовой и символьной индикации для удобства восприятия.

---

## Пример 1: Отчет с критическими замечаниями и блокерами (MR отклонен)

> [!CAUTION]
> ### Статус: 🔴 ОТКЛОНЕНО (Найдены блокеры и критические замечания)
> Merge Request содержит критические ошибки и нарушения архитектурного стиля KBPro. Необходимо исправить замечания уровня `BLOCKER` и `CRITICAL` для повторного прохождения проверки.

### 📋 Список найденных дефектов

---

🛑 **[BLOCKER]**
📍 **Локация**: [DentistryGameModule.cs:L82](file:///Assets/KBPro/Modules/Dentistry/Scripts/DentistryGameModule.cs#L82) (метод `Dispose`)
🔍 **Суть проблемы**: Вызов `base.Dispose();` находится в начале метода `Dispose()`, перед очисткой локальных подписок и ресурсов.
⚠️ **Потенциальные последствия**: Вызов `base.Dispose()` уничтожает базовый контекст и шину событий. Попытка отписаться от событий или очистить локальные системы после этого вызовет `NullReferenceException` и приведет к критической утечке памяти в рантайме.
ℹ️ **Пояснение**: Согласно **Принципу 4** платформы KBPro, вызов базового метода `base.Dispose()` должен находиться строго последней строкой.
🛠️ **Рекомендуемое решение**:
```diff
  public override void Dispose()
  {
-     base.Dispose();
      _compositeDisposable.Clear();
      _eventSubscription.Dispose();
+     base.Dispose();
  }
```

---

🔴 **[CRITICAL]**
📍 **Локация**: [ToothAnimationSystem.cs:L45](file:///Assets/KBPro/Modules/Dentistry/Scripts/Systems/ToothAnimationSystem.cs#L45) (метод `OnUpdate`)
🔍 **Суть проблемы**: Использование `GetComponent` и LINQ-запроса `FirstOrDefault` внутри горячего пути обновлений системы.
⚠️ **Потенциальные последствия**: Метод `OnUpdate()` вызывается каждый кадр. Вызов `GetComponent` и аллокации от LINQ-запросов создадут огромную нагрузку на CPU и частые подвисания игры (микрофризы) из-за сборки мусора (GC).
ℹ️ **Пояснение**: Нарушение правил оптимизации кода C# и Unity (кэширование компонентов, отсутствие аллокаций в горячих путях).
🛠️ **Рекомендуемое решение**:
```diff
  // Внедрите компонент через атрибут или кэшируйте его при инициализации
  [InjectComponent]
  private ToothView _toothView;

  public void OnUpdate()
  {
-     var renderer = GetComponent<SpriteRenderer>();
-     var activeTooth = _teethList.FirstOrDefault(t => t.IsActive);
      
      // Использование кэшированных данных:
      var renderer = _toothView.Renderer;
      // Используйте классический цикл for вместо LINQ:
      Tooth activeTooth = null;
      for (int i = 0; i < _teethList.Count; i++)
      {
          if (_teethList[i].IsActive)
          {
              activeTooth = _teethList[i];
              break;
          }
      }
  }
```

---

🔴 **[CRITICAL]**
📍 **Локация**: [ToothAnimationSystem.cs:L102](file:///Assets/KBPro/Modules/Dentistry/Scripts/Systems/ToothAnimationSystem.cs#L102)
🔍 **Суть проблемы**: Использование `async void` в асинхронном методе `StartAnimation`.
⚠️ **Потенциальные последствия**: Любое исключение внутри `StartAnimation` приведет к падению игры без возможности обработки. Задача может продолжить выполняться в фоне даже после выгрузки модуля, потребляя ресурсы.
ℹ️ **Пояснение**: Нарушение правил асинхронной безопасности рантайма Unity.
🛠️ **Рекомендуемое решение**:
```diff
- public async void StartAnimation(CancellationToken token)
+ public async UniTaskVoid StartAnimation(CancellationToken token)
  {
      await UniTask.Delay(1000, cancellationToken: token);
      // логика
  }
  
  // Вызов метода:
- StartAnimation(token);
+ StartAnimation(token).Forget();
```

---

## Пример 2: Отчет со средними и минорными замечаниями (MR требует доработки)

> [!WARNING]
> ### Статус: 🟡 ТРЕБУЕТ КОРРЕКТИРОВКИ (Одобрено с замечаниями)
> Блокеров и критических ошибок производительности не обнаружено. Однако присутствуют замечания уровня `MEDIUM` и `MINOR`, которые рекомендуется исправить перед слиянием ветки.

### 📋 Список найденных замечаний

---

🟡 **[MEDIUM]**
📍 **Локация**: [ToothView.cs:L150](file:///Assets/KBPro/Modules/Dentistry/Scripts/Views/ToothView.cs#L150)
🔍 **Суть проблемы**: Асинхронный метод `PlaySfxAsync` ожидает завершения звука, но не принимает и **не использует CancellationToken**.
⚠️ **Потенциальные последствия**: Если игрок выйдет из модуля до завершения проигрывания звука, задача продолжит висеть в памяти, вызывая утечку ресурсов.
ℹ️ **Пояснение**: Согласно правилам асинхронного программирования в KBPro, все методы UniTask обязаны принимать и обрабатывать токен отмены.
🛠️ **Рекомендуемое решение**:
```diff
- public async UniTask PlaySfxAsync(string soundId)
+ public async UniTask PlaySfxAsync(string soundId, CancellationToken token)
  {
-     await UniTask.Delay(500);
+     await UniTask.Delay(500, cancellationToken: token);
  }
```

---

🔵 **[MINOR]**
📍 **Локация**: [ScoreSystem.cs:L120](file:///Assets/KBPro/Modules/Dentistry/Scripts/Systems/ScoreSystem.cs#L120-L135)
🔍 **Суть проблемы**: В коде обнаружен неиспользуемый **закомментированный блок логики (строки 120-135)**.
⚠️ **Потенциальные последствия**: Захламляет файл, снижает общую читаемость кода и усложняет будущую поддержку.
ℹ️ **Пояснение**: История изменений сохраняется в системе Git. Мертвый неиспользуемый код должен быть полностью удален.
🛠️ **Рекомендуемое решение**: Удалить закомментированные строки с 120 по 135.
