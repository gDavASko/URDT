using System;
using UnityEngine;

namespace KBP.URDT.TestPoligon.Mechanics2D.Core
{
    /// <summary>
    /// Контракт изолированного монолитного 2D-модуля тестового полигона механик.
    /// </summary>
    public interface IMechanic2DModule
    {
        /// <summary>
        /// Уникальный идентификатор механики (например: M01_SnapToSlot).
        /// </summary>
        string MechanicId { get; }

        /// <summary>
        /// Отображаемое название механики.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Инструкция для тестировщика / игрока.
        /// </summary>
        string Instruction { get; }

        /// <summary>
        /// Флаг успешного завершения механики.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Прогресс выполнения в диапазоне [0..1].
        /// </summary>
        float ProgressNormalized { get; }

        /// <summary>
        /// Инициализация и подготовка игрового поля.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Сброс состояния механики к исходному.
        /// </summary>
        void ResetMechanic();

        /// <summary>
        /// Событие успешного прохождения механики.
        /// </summary>
        event Action<IMechanic2DModule> OnCompleted;

        /// <summary>
        /// Событие изменения прогресса прохождения.
        /// </summary>
        event Action<IMechanic2DModule, float> OnProgressChanged;
    }
}
