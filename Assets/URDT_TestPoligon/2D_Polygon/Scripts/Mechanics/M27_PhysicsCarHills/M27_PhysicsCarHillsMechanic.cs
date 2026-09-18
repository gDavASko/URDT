using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KBP.URDT.TestPoligon.Mechanics2D.Core;

namespace KBP.URDT.TestPoligon.Mechanics2D.M27_PhysicsCarHills
{
    /// <summary>
    /// Механика #27: Машинка с холмиками с полноценной 2D-физикой (Physics 2D Car on Hills в стиле Hill Climb Racing).
    /// Физика:
    /// - Мягкая независимая 2-точечная подвеска с нормалями к склону (без паразитной тряски).
    /// - Настоящий крутящий момент: резкий газ задирает нос (wheelie), тормоз опускает (stoppie).
    /// - Полноценные сальто и перевороты в воздухе (A/D / стрелки).
    /// - Детекция удара крышей о землю и переворотов с красивым крушением и респавном.
    /// - Финиш срабатывает ТОЛЬКО при реальном физическом касании бампером финишного флага на отметке 2200 м.
    /// </summary>
    public class M27_PhysicsCarHillsMechanic : BaseMechanic2DModule
    {
        [Header("Элементы автомобиля")]
        [SerializeField] private RectTransform _carRoot = null;
        [SerializeField] private RectTransform _chassis = null;
        [SerializeField] private RectTransform _rearWheel = null;
        [SerializeField] private RectTransform _frontWheel = null;
        [SerializeField] private RectTransform _finishFlag = null;
        [SerializeField] private RectTransform _terrainContainer = null;

        [Header("Управление")]
        [SerializeField] private Button _btnGas = null;
        [SerializeField] private Button _btnBrake = null;

        [Header("UI телеметрия")]
        [SerializeField] private TMP_Text _speedometerText = null;
        [SerializeField] private TMP_Text _distanceText = null;
        [SerializeField] private Image _progressBarFill = null;
        [SerializeField] private TMP_Text _instructionText = null;

        [Header("Параметры трассы")]
        [SerializeField] private float _startX = -200f;
        [SerializeField] private float _finishX = 2200f;
        [SerializeField] private float _cameraScreenOffsetX = -100f;

        [Header("Параметры физики")]
        [SerializeField] private float _wheelBase = 58f;
        [SerializeField] private float _wheelRadius = 16f;
        [SerializeField] private float _gravity = -720f;
        [SerializeField] private float _maxSpeed = 380f;
        [SerializeField] private float _engineForce = 480f;
        [SerializeField] private float _reverseForce = 180f;
        [SerializeField] private float _brakeForce = 420f;
        [SerializeField] private float _airTorque = 420f;
        [SerializeField] private float _suspensionSpring = 380f;
        [SerializeField] private float _suspensionDamping = 22f;
        [SerializeField] private float _friction = 16f;

        // Состояние физики
        private Vector2 _pos;
        private Vector2 _vel;
        private float _angle; // Угол кузова в градусах
        private float _angularVel; // Угловая скорость в градусах/сек
        private float _wheelRotation;

        private bool _isGasPressed;
        private bool _isBrakePressed;
        private bool _isCrashed;
        private float _crashResetTimer;
        private Vector2 _lastSafeGroundedPos;
        private float _lastSafeAngle;

        private bool _rearGrounded;
        private bool _frontGrounded;
        private bool _flagTouched;
        private float _flagWaveTimer;

        public bool IsAirborne => !_rearGrounded && !_frontGrounded;

        protected override void Awake()
        {
            base.Awake();
            BindControlEvents();
        }

        private void BindControlEvents()
        {
            if (_btnGas != null)
            {
                var trigger = _btnGas.gameObject.GetComponent<EventTrigger>() ?? _btnGas.gameObject.AddComponent<EventTrigger>();
                AddTriggerEntry(trigger, EventTriggerType.PointerDown, (e) => _isGasPressed = true);
                AddTriggerEntry(trigger, EventTriggerType.PointerUp, (e) => _isGasPressed = false);
            }

            if (_btnBrake != null)
            {
                var trigger = _btnBrake.gameObject.GetComponent<EventTrigger>() ?? _btnBrake.gameObject.AddComponent<EventTrigger>();
                AddTriggerEntry(trigger, EventTriggerType.PointerDown, (e) => _isBrakePressed = true);
                AddTriggerEntry(trigger, EventTriggerType.PointerUp, (e) => _isBrakePressed = false);
            }
        }

        private void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(callback));
            trigger.triggers.Add(entry);
        }

        public override void Initialize()
        {
            base.Initialize();
            _pos = new Vector2(_startX, GetTerrainHeight(_startX) + _wheelRadius + 14f);
            _vel = Vector2.zero;
            _angle = 0f;
            _angularVel = 0f;
            _wheelRotation = 0f;
            _isGasPressed = false;
            _isBrakePressed = false;
            _isCrashed = false;
            _crashResetTimer = 0f;
            _flagTouched = false;
            _flagWaveTimer = 0f;
            _lastSafeGroundedPos = _pos;
            _lastSafeAngle = 0f;

            if (_chassis != null)
            {
                Image chImg = _chassis.GetComponent<Image>();
                if (chImg != null) chImg.color = new Color(0.95f, 0.35f, 0.15f, 1f);
            }

            if (_finishFlag != null)
            {
                _finishFlag.localScale = Vector3.one;
                _finishFlag.localRotation = Quaternion.identity;
            }

            UpdateCarTransforms();
            UpdateCamera();
            UpdateUI();
            SetProgress(0f);

            if (_instructionText != null)
            {
                _instructionText.text = "Зажимайте ГАЗ (D) / ТОРМОЗ (A). В воздухе управляйте наклоном машины!";
            }
        }

        public override void ResetMechanic()
        {
            Initialize();
        }

        private void Update()
        {
            if (_flagTouched)
            {
                // Плавное торможение накатом после победы и анимация флага
                _vel *= 0.93f;
                _pos += _vel * Time.deltaTime;
                UpdateCarTransforms();
                UpdateCamera();

                _flagWaveTimer += Time.deltaTime * 6f;
                if (_finishFlag != null)
                {
                    float s = 1.15f + Mathf.Sin(_flagWaveTimer) * 0.15f;
                    _finishFlag.localScale = new Vector3(s, s, 1f);
                    _finishFlag.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_flagWaveTimer) * 12f);
                }
                return;
            }

            if (_isCrashed)
            {
                // Физика отскока и кувырка при аварии
                _crashResetTimer -= Time.deltaTime;
                _vel.y += _gravity * 0.8f * Time.deltaTime;
                _pos += _vel * Time.deltaTime;
                _angle += _angularVel * Time.deltaTime;

                float gY = GetTerrainHeight(_pos.x);
                if (_pos.y < gY + 10f)
                {
                    _pos.y = gY + 10f;
                    _vel = Vector2.zero;
                    _angularVel = 0f;
                }

                UpdateCarTransforms();
                UpdateCamera();

                if (_crashResetTimer <= 0f)
                {
                    RespawnCar();
                }
                return;
            }

            bool gas = _isGasPressed || UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);
            bool brake = _isBrakePressed || UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);

            // Физический шаг с фиксированным субшагом для стабильности
            float dt = Mathf.Min(Time.deltaTime, 0.035f);
            SimulatePhysics(gas, brake, dt);

            UpdateCarTransforms();
            UpdateCamera();
            UpdateUI();

            float totalDist = Mathf.Max(1f, _finishX - _startX);
            float currentDist = Mathf.Clamp(_pos.x - _startX, 0f, totalDist);
            float progress = currentDist / totalDist;
            SetProgress(progress);

            // Проверка физического касания финишного флага
            CheckFinishFlagTouch();
        }

        private void SimulatePhysics(bool gas, bool brake, float dt)
        {
            float rad = _angle * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 up = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

            // Позиции колес
            Vector2 rearHub = _pos - forward * (_wheelBase * 0.5f) - up * 6f;
            Vector2 frontHub = _pos + forward * (_wheelBase * 0.5f) - up * 6f;

            GetTerrainSurface(rearHub.x, out float gYRear, out Vector2 normRear, out Vector2 tangRear);
            GetTerrainSurface(frontHub.x, out float gYFront, out Vector2 normFront, out Vector2 tangFront);

            float penRear = (gYRear + _wheelRadius) - rearHub.y;
            float penFront = (gYFront + _wheelRadius) - frontHub.y;

            // Колеса цепляются за землю только если кузов направлен вверх (Dot(up, normal) > 0.1)
            // Это исключает паразитное выталкивание перевернутой машины вверх колесами!
            _rearGrounded = penRear > 0f && Vector2.Dot(up, normRear) > 0.1f;
            _frontGrounded = penFront > 0f && Vector2.Dot(up, normFront) > 0.1f;

            Vector2 totalForce = Vector2.zero;
            float totalTorque = 0f;

            // Гравитация
            totalForce.y += _gravity;

            // 1. ЗАДНЕЕ КОЛЕСО
            if (_rearGrounded)
            {
                float vn = Vector2.Dot(_vel, normRear);
                float fSpring = penRear * _suspensionSpring;
                float fDamp = -vn * _suspensionDamping;
                float fn = Mathf.Clamp(fSpring + fDamp, 0f, 1500f);

                totalForce += normRear * fn;

                // Момент от нормали заднего колеса (прижимает нос вниз)
                totalTorque -= fn * 0.05f;

                // Мягкое позиционное выталкивание при глубоком проседании
                if (penRear > 6f)
                {
                    _pos += normRear * ((penRear - 6f) * 0.25f);
                }

                float speedTang = Vector2.Dot(_vel, tangRear);

                // Тяга заднего колеса (AWD 60%)
                if (gas)
                {
                    totalForce += tangRear * (_engineForce * 0.60f);
                    // Мягкий импульс ускорения
                    totalTorque += 30f;
                }

                // Тормоз / Задний ход
                if (brake)
                {
                    if (speedTang > 8f)
                    {
                        totalForce -= tangRear * Mathf.Min(_brakeForce, speedTang * 12f);
                    }
                    else
                    {
                        totalForce -= tangRear * _reverseForce;
                    }
                }

                // Трение качения
                totalForce -= tangRear * (Mathf.Sign(speedTang) * _friction);
            }

            // 2. ПЕРЕДНЕЕ КОЛЕСО
            if (_frontGrounded)
            {
                float vn = Vector2.Dot(_vel, normFront);
                float fSpring = penFront * _suspensionSpring;
                float fDamp = -vn * _suspensionDamping;
                float fn = Mathf.Clamp(fSpring + fDamp, 0f, 1500f);

                totalForce += normFront * fn;

                // Момент от нормали переднего колеса (задирает нос вверх)
                totalTorque += fn * 0.05f;

                if (penFront > 6f)
                {
                    _pos += normFront * ((penFront - 6f) * 0.25f);
                }

                float speedTang = Vector2.Dot(_vel, tangFront);

                // Тяга переднего колеса (AWD 40% - позволяет вытягивать машину на крутые горки!)
                if (gas)
                {
                    totalForce += tangFront * (_engineForce * 0.40f);
                }

                // Торможение переднего колеса
                if (brake)
                {
                    if (speedTang > 8f)
                    {
                        totalForce -= tangFront * Mathf.Min(_brakeForce * 0.6f, speedTang * 8f);
                        totalTorque -= 25f;
                    }
                    else
                    {
                        totalForce -= tangFront * (_reverseForce * 0.5f);
                    }
                }

                totalForce -= tangFront * (Mathf.Sign(speedTang) * _friction);
            }

            // 3. ГРАВИТАЦИОННЫЙ ВОЗВРАТ КОЛЕС НА ЗЕМЛЮ (Мягкий естественный возврат 100f)
            if (_rearGrounded && !_frontGrounded)
            {
                // Заднее колесо на земле, перед в воздухе -> мягко возвращаем перед вниз
                totalTorque -= 100f * Mathf.Max(0.1f, Mathf.Cos(rad));
            }
            else if (_frontGrounded && !_rearGrounded)
            {
                // Переднее колесо на земле, зад в воздухе -> мягко возвращаем зад вниз
                totalTorque += 100f * Mathf.Max(0.1f, Mathf.Cos(rad));
            }

            // 4. БАЛАНСИРОВКА В ВОЗДУХЕ (AIR CONTROL / BACKFLIPS / FRONTFLIPS)
            if (IsAirborne)
            {
                if (gas)
                {
                    // Газ задирает нос (сальто назад)
                    totalTorque += _airTorque;
                }
                if (brake)
                {
                    // Тормоз опускает нос (сальто вперед)
                    totalTorque -= _airTorque;
                }
            }

            // Интегрирование скорости
            _vel += totalForce * dt;
            _vel.x = Mathf.Clamp(_vel.x, -50f, _maxSpeed);

            // Интегрирование угловой скорости
            float angularDrag = IsAirborne ? 1.2f : 2.8f;
            _angularVel += totalTorque * dt;
            _angularVel = Mathf.Lerp(_angularVel, 0f, dt * angularDrag);
            _angularVel = Mathf.Clamp(_angularVel, -380f, 380f);

            _pos += _vel * dt;
            _angle += _angularVel * dt;

            // Вращение колес пропорционально скорости
            _wheelRotation -= (_vel.x * dt / _wheelRadius) * Mathf.Rad2Deg;

            // Сохранение безопасной контрольной точки
            if (_rearGrounded && _frontGrounded && Mathf.Abs(_angle) < 25f && _vel.magnitude > 5f)
            {
                _lastSafeGroundedPos = _pos;
                _lastSafeAngle = _angle;
            }

            // 4. ДЕТЕКТОР АВАРИИ И ПЕРЕВОРОТА
            CheckCrashConditions(up, forward);
        }

        private void CheckCrashConditions(Vector2 up, Vector2 forward)
        {
            // Точка крыши / кабины
            Vector2 roofPt = _pos + up * 24f;
            float gRoof = GetTerrainHeight(roofPt.x);

            // 1. Удар крышей о землю
            if (roofPt.y <= gRoof + 4f)
            {
                TriggerCrash("КРУШЕНИЕ! Удар крышей о землю! Балансируйте в воздухе.");
                return;
            }

            // 2. Сильный наклон (более 76 градусов) при контакте корпуса с землей
            float normAngle = Mathf.Repeat(_angle + 180f, 360f) - 180f;
            if (Mathf.Abs(normAngle) > 76f)
            {
                float gCenter = GetTerrainHeight(_pos.x);
                if (_pos.y <= gCenter + 16f)
                {
                    TriggerCrash("КРУШЕНИЕ! Автомобиль перевернулся! Выравнивайте кузов.");
                    return;
                }
            }

            // 3. Падение глубоко в пропасть
            float gPos = GetTerrainHeight(_pos.x);
            if (_pos.y < gPos - 35f)
            {
                TriggerCrash("КРУШЕНИЕ! Вылет с трассы.");
            }
        }

        private void TriggerCrash(string reason)
        {
            if (_isCrashed) return;
            _isCrashed = true;
            _crashResetTimer = 1.35f;

            // Динамичный отскок от удара
            _vel = new Vector2(-35f, 65f);
            float normAngle = Mathf.Repeat(_angle + 180f, 360f) - 180f;
            _angularVel = Mathf.Sign(normAngle) * 240f;

            if (_chassis != null)
            {
                Image chImg = _chassis.GetComponent<Image>();
                if (chImg != null) chImg.color = new Color(1f, 0.2f, 0.2f, 1f);
            }

            if (_instructionText != null)
            {
                _instructionText.text = $"<color=#FF4444>{reason}</color>";
            }
        }

        private void RespawnCar()
        {
            _isCrashed = false;
            _pos = _lastSafeGroundedPos + new Vector2(0f, 18f);
            _vel = Vector2.zero;
            _angle = _lastSafeAngle;
            _angularVel = 0f;

            if (_chassis != null)
            {
                Image chImg = _chassis.GetComponent<Image>();
                if (chImg != null) chImg.color = new Color(0.95f, 0.35f, 0.15f, 1f);
            }

            if (_instructionText != null)
            {
                _instructionText.text = "Зажимайте ГАЗ / ТОРМОЗ. Удерживайте машину на колесах!";
            }
        }

        private void CheckFinishFlagTouch()
        {
            float rad = _angle * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 frontBumper = _pos + forward * 38f;

            float flagGround = GetTerrainHeight(_finishX);

            // Финиш засчитывается ТОЛЬКО если передний бампер машины физически достиг координаты флага
            // и машина находится в пределах высоты флагштока (не пролетает высоко в стратосфере)
            bool reachedX = frontBumper.x >= _finishX;
            bool reachedY = _pos.y >= (flagGround - 15f) && _pos.y <= (flagGround + 60f);

            if (reachedX && reachedY)
            {
                _flagTouched = true;
                CompleteMechanic();

                if (_instructionText != null)
                {
                    _instructionText.text = "<color=#00FF99>ПОБЕДА! Вы коснулись финишного флага и преодолели трассу 2200 м!</color>";
                }
            }
        }

        private void UpdateCarTransforms()
        {
            if (_carRoot != null)
            {
                _carRoot.anchoredPosition = _pos;
                // Вся машина (кузов и колеса) поворачивается как единое твердое тело!
                _carRoot.localRotation = Quaternion.Euler(0f, 0f, _angle);
            }

            if (_chassis != null)
            {
                _chassis.localRotation = Quaternion.identity;
            }

            if (_rearWheel != null)
            {
                _rearWheel.localRotation = Quaternion.Euler(0f, 0f, _wheelRotation);
            }

            if (_frontWheel != null)
            {
                _frontWheel.localRotation = Quaternion.Euler(0f, 0f, _wheelRotation);
            }
        }

        private void UpdateCamera()
        {
            if (_terrainContainer != null)
            {
                float targetCamX = -_pos.x + _cameraScreenOffsetX;
                float targetCamY = -Mathf.Clamp(_pos.y + 35f, -70f, 90f) * 0.35f;
                _terrainContainer.anchoredPosition = new Vector2(targetCamX, targetCamY);
            }
        }

        public static void GetTerrainSurface(float x, out float height, out Vector2 normal, out Vector2 tangent)
        {
            height = GetTerrainHeight(x);
            float hNext = GetTerrainHeight(x + 2f);
            float dy = hNext - height;
            float dx = 2f;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            normal = new Vector2(-dy / len, dx / len);
            tangent = new Vector2(dx / len, dy / len);
        }

        /// <summary>
        /// Непрерывная аналитическая функция холмистой трассы протяженностью 2200 метров.
        /// Исключены обрывы, ступени и ямы: плавный разгон со старта и мягкий выезд на финиш.
        /// </summary>
        public static float GetTerrainHeight(float x)
        {
            const float baseGround = -35f;

            // Плавный разгон на старте (от -350 до +80)
            float tStart = Mathf.Clamp01((x - (-60f)) / 140f);
            float blendStart = tStart * tStart * (3f - 2f * tStart); // SmoothStep

            // Плавный выезд на финишное плато (от 2100 до 2220)
            float tFinish = Mathf.Clamp01((x - 2100f) / 120f);
            float blendFinish = 1f - (tFinish * tFinish * (3f - 2f * tFinish)); // SmoothStep

            float hillBlend = Mathf.Min(blendStart, blendFinish);

            // Мягкие, приятные холмы (угол наклона не превышает 30 градусов)
            float h1 = Mathf.Sin(x * 0.006f) * 32f;
            float h2 = Mathf.Sin(x * 0.015f + 0.5f) * 18f;
            float h3 = Mathf.Cos(x * 0.028f) * 10f;

            // 3 трамплина для длинных зрелищных прыжков
            float ramp1 = Mathf.Exp(-Mathf.Pow((x - 520f) / 55f, 2)) * 36f;
            float ramp2 = Mathf.Exp(-Mathf.Pow((x - 1120f) / 60f, 2)) * 42f;
            float ramp3 = Mathf.Exp(-Mathf.Pow((x - 1720f) / 55f, 2)) * 38f;

            float hills = (h1 + h2 + h3 + ramp1 + ramp2 + ramp3) * hillBlend;
            return baseGround + hills;
        }

        private void UpdateUI()
        {
            if (_speedometerText != null)
            {
                float speedKmH = Mathf.Max(0f, _vel.magnitude * 0.28f);
                string status = IsAirborne ? " [В ВОЗДУХЕ!]" : "";
                _speedometerText.text = $"Скорость: {speedKmH:F0} км/ч{status}";
                _speedometerText.color = IsAirborne ? new Color(0.3f, 0.9f, 1f) : Color.white;
            }

            if (_distanceText != null)
            {
                float totalDist = Mathf.Max(1f, _finishX - _startX);
                float currentDist = Mathf.Clamp(_pos.x - _startX, 0f, totalDist);
                _distanceText.text = $"Дистанция: {currentDist:F0} / {totalDist:F0} м";
            }

            if (_progressBarFill != null)
            {
                float totalDist = Mathf.Max(1f, _finishX - _startX);
                float currentDist = Mathf.Clamp(_pos.x - _startX, 0f, totalDist);
                _progressBarFill.fillAmount = Mathf.Clamp01(currentDist / totalDist);
            }
        }
    }
}
