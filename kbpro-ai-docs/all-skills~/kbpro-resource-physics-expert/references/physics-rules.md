# Правила работы с физикой в Unity

Этот справочник описывает стандарты оптимизации и проектирования физического движка (2D/3D) в проектах KBPro.

---

## 1. Управление Rigidbody через FixedUpdate

Любое перемещение или поворот физического тела (`Rigidbody` / `Rigidbody2D`) должно выполняться внутри перегрузки `FixedUpdate` (для систем — в `IFixedUpdatable`), используя специализированные методы позиционирования:

- **Запрещено** перемещать физическое тело через изменение `transform.position`. Это ломает физические расчеты столкновений и вызывает визуальные рывки (jitter).
- Используйте методы `MovePosition()` и `MoveRotation()` для плавного перемещения с физической интерполяцией.

```csharp
using KBP.CORE;
using UnityEngine;

public class PhysicsMovementSystem : LogicSystem, IFixedUpdatable
{
    [InjectComponent] private PlayerComponent _player = null; // Хранит Rigidbody

    private Vector2 _moveInput;
    private float _speed = 5f;

    public void SetInput(Vector2 input) => _moveInput = input.normalized;

    // Вызывается в цикле FixedUpdate
    public void OnFixedUpdate()
    {
        var rb = _player.Rigidbody;
        
        // Расчет целевой позиции
        Vector3 targetPos = rb.position + (Vector3)_moveInput * _speed * Time.fixedDeltaTime;
        
        // Плавное физическое перемещение
        rb.MovePosition(targetPos);
    }
}
```

---

## 2. Физическая интерполяция (Interpolation)

Если за вашим физическим объектом на сцене следует камера (Cinemachine) или другой скрипт слежения, вы обязаны включить интерполяцию на `Rigidbody`:
- Установите `Rigidbody.interpolation` в значение `Interpolate` (для плавности интерполяции на основе кадров отрисовки) или `Extrapolate` (для прогнозирования).
- Это гарантирует отсутствие "микро-дерганий" объекта при движении камеры.

---

## 3. Исключение аллокаций (NonAlloc-запросы)

Частые физические запросы (лучи, сферы, сенсоры), вызываемые каждый кадр в методах обновления (`Update` / `FixedUpdate`), обязаны использовать `NonAlloc` версии API с предварительно выделенным буфером результатов.

- Стандартные методы (например, `Physics2D.OverlapCircleAll`) выделяют память в куче (Garbage) на каждый вызов, что приводит к фризам игры из-за сборщика мусора.

```csharp
using KBP.CORE;
using UnityEngine;

public class SensorSystem : LogicSystem, IUpdatable
{
    [InjectComponent] private SensorComponent _sensor = null;

    private readonly Collider2D[] _resultsBuffer = new Collider2D[10]; // Буфер результатов

    public void OnUpdate()
    {
        Vector2 sensorPos = _sensor.transform.position;
        float radius = _sensor.DetectionRadius;

        // Использование NonAlloc-версии для поиска перекрытий без создания мусора в куче
        int hitCount = Physics2D.OverlapCircleNonAlloc(sensorPos, radius, _resultsBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = _resultsBuffer[i];
            ProcessDetection(col);
        }
    }

    private void ProcessDetection(Collider2D col) { /* ... */ }
}
```

---

## 4. Использование Collision Layer Matrix

Вместо написания кода проверки тегов при столкновениях в `OnCollisionEnter` / `OnTriggerEnter`:
1. Разделите типы объектов по различным физическим слоям (Layers) в Unity (например, `Player`, `Enemy`, `PlayerProjectile`, `Obstacle`).
2. В окне настроек **Project Settings -> Physics / Physics 2D** настройте матрицу пересечений слоев (**Layer Collision Matrix**), сняв галочки со слоев, которые не должны взаимодействовать.
3. Это перенесет расчеты коллизий на нативный уровень движка PhysX, разгрузив CPU от вызовов `OnTriggerEnter` для нерелевантных столкновений.
