using UnityEngine;

namespace Game.Gameplay.PhysicsJuice
{
    public static class PhysicsMaterialFactory
    {
        private static PhysicsMaterial _bouncyRubber;
        private static PhysicsMaterial _slipperyIce;
        private static PhysicsMaterial _heavyMetal;
        private static PhysicsMaterial _comedicNormal;

        public static PhysicsMaterial BouncyRubber
        {
            get
            {
                if (_bouncyRubber == null)
                {
                    _bouncyRubber = new PhysicsMaterial("BouncyRubber")
                    {
                        bounciness = 0.85f,
                        bounceCombine = PhysicsMaterialCombine.Maximum,
                        dynamicFriction = 0.35f,
                        staticFriction = 0.35f,
                        frictionCombine = PhysicsMaterialCombine.Average
                    };
                }
                return _bouncyRubber;
            }
        }

        public static PhysicsMaterial SlipperyIce
        {
            get
            {
                if (_slipperyIce == null)
                {
                    _slipperyIce = new PhysicsMaterial("SlipperyIce")
                    {
                        bounciness = 0.08f,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        dynamicFriction = 0.02f,
                        staticFriction = 0.02f,
                        frictionCombine = PhysicsMaterialCombine.Minimum
                    };
                }
                return _slipperyIce;
            }
        }

        public static PhysicsMaterial HeavyMetal
        {
            get
            {
                if (_heavyMetal == null)
                {
                    _heavyMetal = new PhysicsMaterial("HeavyMetal")
                    {
                        bounciness = 0.15f,
                        bounceCombine = PhysicsMaterialCombine.Average,
                        dynamicFriction = 0.65f,
                        staticFriction = 0.75f,
                        frictionCombine = PhysicsMaterialCombine.Multiply
                    };
                }
                return _heavyMetal;
            }
        }

        public static PhysicsMaterial ComedicNormal
        {
            get
            {
                if (_comedicNormal == null)
                {
                    _comedicNormal = new PhysicsMaterial("ComedicNormal")
                    {
                        bounciness = 0.35f,
                        bounceCombine = PhysicsMaterialCombine.Average,
                        dynamicFriction = 0.45f,
                        staticFriction = 0.5f,
                        frictionCombine = PhysicsMaterialCombine.Average
                    };
                }
                return _comedicNormal;
            }
        }

        public static void ApplyBouncy(Collider collider)
        {
            if (collider != null) collider.material = BouncyRubber;
        }

        public static void ApplySlippery(Collider collider)
        {
            if (collider != null) collider.material = SlipperyIce;
        }

        public static void ApplyHeavy(Collider collider)
        {
            if (collider != null) collider.material = HeavyMetal;
        }
    }
}
