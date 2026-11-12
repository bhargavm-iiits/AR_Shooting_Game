using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityDefender
{
    /// <summary>
    /// Layered, pooled orb explosion built from mobile-cheap particle systems (~90 particles, additive, no textures):
    /// white-hot flash, magenta plasma core, fast stretched sparks with drag and gravity, twin expanding shockwave
    /// rings, drifting embers and a fading plasma haze. All sizes and speeds are authored for a 0.14 m orb and scaled
    /// per hit, so the blast stays proportional when the arena is resized.
    /// </summary>
    public sealed class EnergyBlast : MonoBehaviour
    {
        sealed class Layer
        {
            public ParticleSystem system;
            public Vector2 speed, size;
            public float radius, gravity, noise;
        }

        readonly List<Layer> layers = new List<Layer>(8);
        float activeUntil;
        const float Lifetime = 1.6f;

        public bool IsPlaying => gameObject.activeSelf;

        public static EnergyBlast Create(Material glow, Material ring)
        {
            var root = new GameObject("Energy blast");
            var blast = root.AddComponent<EnergyBlast>();
            Color white = Color.white;
            Color cyan = new Color(0.35f, 0.92f, 1f);
            Color magenta = new Color(1f, 0.25f, 0.95f);
            Color violet = new Color(0.45f, 0.18f, 1f);

            // 1. Flash: one white-hot sphere of light that swells and vanishes in ~0.1 s.
            blast.AddLayer("Flash", glow, 1, new Vector2(.09f, .12f), Vector2.zero, new Vector2(.44f, .44f), 0f, 0f,
                Fade(new[] { (white, 0f), (cyan, 1f) }, new[] { (1f, 0f), (0.9f, 0.35f), (0f, 1f) }),
                Curve((0f, .35f), (.3f, 1f), (1f, 1.3f)));
            // 2. Plasma core: dense magenta-violet blobs that burst outward then collapse.
            blast.AddLayer("Plasma core", glow, 14, new Vector2(.22f, .42f), new Vector2(.15f, .55f), new Vector2(.07f, .15f), .03f, 0f,
                Fade(new[] { (white, 0f), (magenta, .3f), (violet, 1f) }, new[] { (1f, 0f), (.9f, .3f), (0f, 1f) }),
                Curve((0f, .6f), (.25f, 1.1f), (1f, .15f)), drag: 2.2f);
            // 3. Sparks: fast streaks that slow down and fall.
            blast.AddLayer("Sparks", glow, 42, new Vector2(.25f, .6f), new Vector2(1.6f, 3.6f), new Vector2(.008f, .016f), .04f, .35f,
                Fade(new[] { (white, 0f), (cyan, .35f), (violet, 1f) }, new[] { (1f, 0f), (1f, .5f), (0f, 1f) }),
                Curve((0f, 1f), (1f, 0f)), stretch: true, drag: 1.6f);
            // 4. Primary shockwave ring.
            blast.AddLayer("Shockwave", ring, 1, new Vector2(.3f, .3f), Vector2.zero, new Vector2(.14f, .14f), 0f, 0f,
                Fade(new[] { (cyan, 0f), (violet, 1f) }, new[] { (.95f, 0f), (.6f, .5f), (0f, 1f) }),
                Curve((0f, .6f), (.35f, 4.5f), (1f, 6.5f)));
            // 5. Trailing magenta shockwave.
            blast.AddLayer("Shockwave 2", ring, 1, new Vector2(.4f, .4f), Vector2.zero, new Vector2(.14f, .14f), 0f, 0f,
                Fade(new[] { (magenta, 0f), (violet, 1f) }, new[] { (.7f, 0f), (.4f, .5f), (0f, 1f) }),
                Curve((0f, .4f), (.4f, 3.4f), (1f, 5f)), delay: .05f);
            // 6. Embers: slow glowing motes that drift on turbulence and fade.
            blast.AddLayer("Embers", glow, 22, new Vector2(.7f, 1.4f), new Vector2(.25f, .9f), new Vector2(.01f, .022f), .05f, -.02f,
                Fade(new[] { (new Color(1f, .6f, 1f), 0f), (magenta, .4f), (new Color(.2f, .4f, 1f), 1f) }, new[] { (1f, 0f), (.8f, .6f), (0f, 1f) }),
                Curve((0f, 1f), (.7f, .7f), (1f, .2f)), drag: .8f, noise: .35f, delay: .03f);
            // 7. Plasma haze: large dim clouds that linger after the flash.
            blast.AddLayer("Plasma haze", glow, 6, new Vector2(.5f, .9f), new Vector2(.05f, .2f), new Vector2(.18f, .3f), .03f, 0f,
                Fade(new[] { (violet, 0f), (new Color(.15f, .35f, .9f), 1f) }, new[] { (.35f, 0f), (.2f, .5f), (0f, 1f) }),
                Curve((0f, .5f), (1f, 1.6f)), rotate: 60f);
            root.SetActive(false);
            return blast;
        }

        public void Play(Vector3 position, float scale)
        {
            scale = Mathf.Max(0.05f, scale);
            transform.position = position;
            gameObject.SetActive(true);
            foreach (Layer layer in layers)
            {
                var main = layer.system.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(layer.speed.x * scale, layer.speed.y * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(layer.size.x * scale, layer.size.y * scale);
                main.gravityModifier = layer.gravity * scale;
                var shape = layer.system.shape;
                if (shape.enabled) shape.radius = layer.radius * scale;
                if (layer.noise > 0f) { var noise = layer.system.noise; noise.strength = layer.noise * scale; }
                layer.system.Clear(true);
                layer.system.Play(true);
            }
            activeUntil = Time.time + Lifetime;
        }

        public void Stop()
        {
            foreach (Layer layer in layers) layer.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (Time.time >= activeUntil) Stop();
        }

        void AddLayer(string name, Material material, int count, Vector2 life, Vector2 speed, Vector2 size, float radius, float gravity,
            Gradient color, AnimationCurve sizeCurve, bool stretch = false, float drag = 0f, float noise = 0f, float delay = 0f, float rotate = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = count;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = gravity;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });

            var shape = system.shape;
            shape.enabled = radius > 0f;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(radius, 0.0001f);

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = color;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            if (drag > 0f)
            {
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }
            if (noise > 0f)
            {
                var turbulence = system.noise;
                turbulence.enabled = true;
                turbulence.strength = noise;
                turbulence.frequency = 2f;
                turbulence.scrollSpeed = 1f;
                turbulence.quality = ParticleSystemNoiseQuality.Medium;
            }
            if (rotate != 0f)
            {
                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-rotate * Mathf.Deg2Rad, rotate * Mathf.Deg2Rad);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.07f;
                renderer.lengthScale = 1.5f;
            }
            else renderer.renderMode = ParticleSystemRenderMode.Billboard;

            layers.Add(new Layer { system = system, speed = speed, size = size, radius = radius, gravity = gravity, noise = noise });
        }

        static Gradient Fade((Color color, float time)[] colors, (float alpha, float time)[] alphas)
        {
            var gradient = new Gradient();
            var colorKeys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) colorKeys[i] = new GradientColorKey(colors[i].color, colors[i].time);
            var alphaKeys = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) alphaKeys[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        static AnimationCurve Curve(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var key in keys) curve.AddKey(key.time, key.value);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
