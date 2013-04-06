/*******************************************************************************
 * Original Java code:
 * Copyright (c) 2013, Daniel Murphy
 * All rights reserved.
 ******************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FluidPort
{
    public class FluidSimulation
    {
        public const int MAX_PARTICLES = 5000;
        public const int MAX_NEIGHBORS = 75;
        public const float CELL_SIZE = 0.5f;
        public const float RADIUS = 0.6f;
        public const float VISCOSITY = 0.004f;
        public const float IDEAL_RADIUS = 50f;
        public const float IDEAL_RADIUS_SQ = IDEAL_RADIUS * IDEAL_RADIUS;
        public const float MULTIPLIER = IDEAL_RADIUS / RADIUS;
        public const float DT = 1f / 60f;
        private int _numActiveParticles = 0;
        private Particle[] _liquid;
        private List<int> _activeParticles;
        private Vector2[] _delta;
        private Vector2[] _scaledPositions;
        private Vector2[] _scaledVelocities;
        private Dictionary<int, Dictionary<int, List<int>>> _grid;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private SpriteFont _font;
        private float _scale = 32f;
        private Vector2 _mouse;
        private Random _random;

        private int getGridX(float x) { return (int)Math.Floor(x / CELL_SIZE); }
        private int getGridY(float y) { return (int)Math.Floor(y / CELL_SIZE); }

        public FluidSimulation(SpriteBatch spriteBatch, SpriteFont font)
        {
            _spriteBatch = spriteBatch;
            _font = font;
            _activeParticles = new List<int>(MAX_PARTICLES);
            _liquid = new Particle[MAX_PARTICLES];
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                _liquid[i] = new Particle(Vector2.Zero, Vector2.Zero, false);
                _liquid[i].index = i;
            }

            _delta = new Vector2[MAX_PARTICLES];
            _scaledPositions = new Vector2[MAX_PARTICLES];
            _scaledVelocities = new Vector2[MAX_PARTICLES];

            _grid = new Dictionary<int, Dictionary<int, List<int>>>();

            _pixel = new Texture2D(_spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData<Color>(new[] { Color.White });

            _random = new Random();
        }

        private void findNeighbors(Particle particle)
        {
            particle.neighborCount = 0;
            Dictionary<int, List<int>> gridX;
            List<int> gridY;

            for (int nx = -1; nx < 2; nx++)
            {
                for (int ny = -1; ny < 2; ny++)
                {
                    int x = particle.ci + nx;
                    int y = particle.cj + ny;
                    if (_grid.TryGetValue(x, out gridX) && gridX.TryGetValue(y, out gridY))
                    {

                        for (int a = 0; a < gridY.Count; a++)
                        {
                            if (gridY[a] != particle.index)
                            {
                                particle.neighbors[particle.neighborCount] = gridY[a];
                                particle.neighborCount++;

                                if (particle.neighborCount >= MAX_NEIGHBORS)
                                    return;
                            }
                        }
                    }
                }
            }
        }

        private void applyLiquidConstraints()
        {
            // Prepare simulation
            for (int i = 0; i < _numActiveParticles; i++)
            {
                int index = _activeParticles[i];
                Particle particle = _liquid[index];

                // Scale positions and velocities
                _scaledPositions[index] = particle.position * MULTIPLIER;
                _scaledVelocities[index] = particle.velocity * MULTIPLIER;

                // Reset deltas
                _delta[index] = Vector2.Zero;
            }

            for (int i = 0; i < _numActiveParticles; i++)
            {
                int index = _activeParticles[i];
                Particle particle = _liquid[index];

                // Find neighbors
                findNeighbors(particle);

                // Calculate pressure
                float p = 0.0f;
                float pnear = 0.0f;
                for (int a = 0; a < particle.neighborCount; a++)
                {
                    Vector2 relativePosition = _scaledPositions[particle.neighbors[a]] - _scaledPositions[index];
                    float distanceSq = relativePosition.LengthSquared();

                    //within idealRad check
                    if (distanceSq < IDEAL_RADIUS_SQ)
                    {
                        particle.distances[a] = (float)Math.Sqrt(distanceSq);
                        //if (particle.distances[a] < Settings.EPSILON) particle.distances[a] = IDEAL_RADIUS - .01f;
                        float oneminusq = 1.0f - (particle.distances[a] / IDEAL_RADIUS);
                        p = (p + oneminusq * oneminusq);
                        pnear = (pnear + oneminusq * oneminusq * oneminusq);
                    }
                    else
                    {
                        particle.distances[a] = float.MaxValue;
                    }
                }

                // Calculate forces
                float pressure = (p - 5f) / 2.0f; //normal pressure term
                float presnear = pnear / 2.0f; //near particles term
                Vector2 change = Vector2.Zero;
                for (int a = 0; a < particle.neighborCount; a++)
                {
                    Vector2 relativePosition = _scaledPositions[particle.neighbors[a]] - _scaledPositions[index];

                    if (particle.distances[a] < IDEAL_RADIUS)
                    {
                        float q = particle.distances[a] / IDEAL_RADIUS;
                        float oneminusq = 1.0f - q;
                        float factor = oneminusq * (pressure + presnear * oneminusq) / (2.0F * particle.distances[a]);
                        Vector2 d = relativePosition * factor;
                        Vector2 relativeVelocity = _scaledVelocities[particle.neighbors[a]] - _scaledVelocities[index];

                        factor = VISCOSITY * oneminusq * DT;
                        d -= relativeVelocity * factor;
                        _delta[particle.neighbors[a]] += d;
                        change -= d;
                    }
                }
                _delta[index] += change;
            }

            // Move particles
            for (int i = 0; i < _numActiveParticles; i++)
            {
                int index = _activeParticles[i];
                Particle particle = _liquid[index];

                particle.position += _delta[index] / MULTIPLIER;
                particle.velocity += _delta[index] / (MULTIPLIER * DT);

                /*
                // NaN/Infinity Tests
                Debug.Assert(!float.IsInfinity(particle.position.X));
                Debug.Assert(!float.IsInfinity(particle.position.Y));
                Debug.Assert(!float.IsInfinity(particle.velocity.X));
                Debug.Assert(!float.IsInfinity(particle.velocity.Y));
                Debug.Assert(!float.IsNaN(particle.position.X));
                Debug.Assert(!float.IsNaN(particle.position.Y));
                Debug.Assert(!float.IsNaN(particle.velocity.X));
                Debug.Assert(!float.IsNaN(particle.velocity.Y));
                */

                // Update particle cell
                int x = getGridX(particle.position.X);
                int y = getGridY(particle.position.Y);

                if (particle.ci == x && particle.cj == y)
                    continue;
                else
                {
                    _grid[particle.ci][particle.cj].Remove(index);

                    if (_grid[particle.ci][particle.cj].Count == 0)
                    {
                        _grid[particle.ci].Remove(particle.cj);

                        if (_grid[particle.ci].Count == 0)
                        {
                            _grid.Remove(particle.ci);
                        }
                    }

                    if (!_grid.ContainsKey(x))
                        _grid[x] = new Dictionary<int, List<int>>();
                    if (!_grid[x].ContainsKey(y))
                        _grid[x][y] = new List<int>(20);

                    _grid[x][y].Add(index);
                    particle.ci = x;
                    particle.cj = y;
                }
            }
        }

        public void createParticle(int numParticlesToSpawn = 4)
        {
            IEnumerable<Particle> inactiveParticles = from particle in _liquid
                                                        where particle.alive == false
                                                        select particle;
            inactiveParticles = inactiveParticles.Take(numParticlesToSpawn);

            foreach (Particle particle in inactiveParticles)
            {
                if (_numActiveParticles < MAX_PARTICLES)
                {
                    Vector2 jitter = new Vector2((float)(_random.NextDouble() * 2 - 1), (float)(_random.NextDouble()) - 0.5f);

                    particle.position = _mouse + jitter;
                    particle.velocity = Vector2.Zero;
                    particle.alive = true;
                    particle.ci = getGridX(particle.position.X);
                    particle.cj = getGridY(particle.position.Y);

                    // Create grid cell if necessary
                    if (!_grid.ContainsKey(particle.ci))
                        _grid[particle.ci] = new Dictionary<int, List<int>>();
                    if (!_grid[particle.ci].ContainsKey(particle.cj))
                        _grid[particle.ci][particle.cj] = new List<int>();
                    _grid[particle.ci][particle.cj].Add(particle.index);

                    _activeParticles.Add(particle.index);
                    _numActiveParticles++;
                }
            }
        }

        public void update()
        {
            MouseState mouseState = Mouse.GetState();

            _mouse = new Vector2(mouseState.X, mouseState.Y) / _scale;

            if (mouseState.LeftButton == ButtonState.Pressed)
                createParticle();

            applyLiquidConstraints();
        }

        public void draw()
        {
            _spriteBatch.Begin();

            for (int i = 0; i < _numActiveParticles; i++)
            {
                Particle particle = _liquid[_activeParticles[i]];

                _spriteBatch.Draw(_pixel, particle.position * _scale, new Rectangle(0, 0, 2, 2), Color.LightBlue, 0f, new Vector2(1, 1), 1f, SpriteEffects.None, 0f);
            }

            string text = String.Format("MAX_PARTICLES: {0} \nnumActiveParticles: {1}", MAX_PARTICLES, _numActiveParticles);
            _spriteBatch.DrawString(_font, text, new Vector2(8, 8), Color.White);

            _spriteBatch.End();
        }
    }
}
