using NUnit.Framework;
using Natori.CityBuilder.Editor;
using UnityEngine;

namespace Natori.CityBuilder.Tests
{
    public sealed class NatoriCityRotationDragTests
    {
        [Test]
        public void DragAppliesOnlyNewQuarterTurnsAcrossAngleWrap()
        {
            var drag = new NatoriCityRotationDrag();
            Assert.That(drag.Update(Quaternion.Euler(0, 30, 0)), Is.Zero);
            Assert.That(drag.Update(Quaternion.Euler(0, 90, 0)), Is.EqualTo(1));
            drag.Accept(1);
            Assert.That(drag.Update(Quaternion.Euler(0, 100, 0)), Is.Zero);
            Assert.That(drag.Update(Quaternion.Euler(0, 180, 0)), Is.EqualTo(1));
            drag.Accept(1);
            Assert.That(drag.Update(Quaternion.Euler(0, -90, 0)), Is.EqualTo(1));
            drag.Accept(1);
            Assert.That(drag.Update(Quaternion.identity), Is.EqualTo(1));
            drag.Accept(1);
            Assert.That(drag.Update(Quaternion.identity), Is.Zero);
        }

        [Test]
        public void RejectedAngleDoesNotAdvanceAppliedRotation()
        {
            var drag = new NatoriCityRotationDrag();
            Assert.That(drag.Update(Quaternion.Euler(0, 90, 0)), Is.EqualTo(1));
            //配置検証に失敗したためAcceptしない。元の角へ戻す操作はモデル変更を要求しない。
            Assert.That(drag.Update(Quaternion.identity), Is.Zero);
            Assert.That(drag.Update(Quaternion.Euler(0, -90, 0)), Is.EqualTo(-1));
            drag.Accept(-1);
            Assert.That(drag.Update(Quaternion.Euler(0, -95, 0)), Is.Zero);
            drag.Reset();
            Assert.That(drag.HandleRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(drag.Update(Quaternion.Euler(0, 90, 0)), Is.EqualTo(1));
        }
    }
}
