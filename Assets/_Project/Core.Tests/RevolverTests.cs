using System;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Randomness;
using NUnit.Framework;

namespace LiarsBar.Core.Tests
{
    public class RevolverTests
    {
        [Test]
        public void Shot_HitsExactlyOnBulletChamber([Range(0, 5)] int bulletIndex)
        {
            var revolver = new Revolver(bulletIndex);

            for (var i = 0; i < bulletIndex; i++)
                Assert.IsFalse(revolver.Shot(), $"Chamber {i} must be empty.");

            Assert.IsTrue(revolver.Shot(), $"Chamber {bulletIndex} must fire.");
            Assert.IsTrue(revolver.IsFired);
            Assert.AreEqual(bulletIndex + 1, revolver.Shooted);
        }

        [Test]
        public void Shot_FiresNoLaterThanSixthPull_ForAnySeed()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var revolver = Revolver.CreateRandom(new SeededRandom(seed));
                var pulls = 0;
                var fired = false;
                while (!fired && pulls < Revolver.DefaultChambers)
                {
                    fired = revolver.Shot();
                    pulls++;
                }
                Assert.IsTrue(fired, $"Seed {seed}: no shot after {pulls} pulls.");
            }
        }

        [Test]
        public void CreateRandom_CoversAllChambers()
        {
            var random = new SeededRandom(42);
            var seen = new bool[Revolver.DefaultChambers];
            for (var i = 0; i < 300; i++)
                seen[Revolver.CreateRandom(random).BulletIndex] = true;

            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void Shot_AfterFired_Throws()
        {
            var revolver = new Revolver(0);
            revolver.Shot();
            Assert.Throws<InvalidOperationException>(() => revolver.Shot());
        }

        [Test]
        public void Constructor_RejectsInvalidBullet()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Revolver(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Revolver(6));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var revolver = new Revolver(3);
            revolver.Shot();
            var clone = revolver.Clone();
            clone.Shot();

            Assert.AreEqual(1, revolver.Shooted);
            Assert.AreEqual(2, clone.Shooted);
            Assert.AreEqual(revolver.BulletIndex, clone.BulletIndex);
        }

        [Test]
        public void Clone_OfFiredRevolver_StaysFired()
        {
            var revolver = new Revolver(0);
            revolver.Shot();
            var clone = revolver.Clone();

            Assert.IsTrue(clone.IsFired);
            Assert.AreEqual(revolver.Shooted, clone.Shooted);
        }
    }
}
