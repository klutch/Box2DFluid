using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using FarseerPhysics.Collision.Shapes;

namespace FluidPort
{
    public class Game1 : Game
    {
        public const float SCALE = 35f;
        public const float DT = 1f / 60f;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private FluidSimulation _fluidSimulation;
        private SpriteFont _debugFont;
        private World _world;
        private DebugViewXNA _debugView;
        private Matrix _projection;
        private Matrix _view;

        public Game1()
        {
            IsMouseVisible = true;
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            _world = new World(new Vector2(0, 9.8f));
            _debugView = new DebugViewXNA(_world);
            _debugView.AppendFlags(FarseerPhysics.DebugViewFlags.Shape);
            _debugView.DefaultShapeColor = Color.White;
            _debugView.SleepingShapeColor = Color.LightGray;
            _debugView.LoadContent(GraphicsDevice, Content);

            base.Initialize();

            _fluidSimulation = new FluidSimulation(_world, _spriteBatch, _debugFont);
            _projection = Matrix.CreateOrthographic(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 1);
            _view = Matrix.CreateScale(new Vector3(SCALE, -SCALE, 1));

            // Create test geometry
            Body bottomBody = BodyFactory.CreateBody(_world, new Vector2(0, 7f));
            PolygonShape bottomShape = new PolygonShape(1f);
            bottomShape.SetAsBox(12f, 1f);
            bottomBody.CreateFixture(bottomShape);

            Body leftBody = BodyFactory.CreateBody(_world, new Vector2(-11f, 0f));
            PolygonShape leftShape = new PolygonShape(1f);
            leftShape.SetAsBox(1f, 10f);
            leftBody.CreateFixture(leftShape);

            Body rightBody = BodyFactory.CreateBody(_world, new Vector2(11f, 0f));
            PolygonShape rightShape = new PolygonShape(1f);
            rightShape.SetAsBox(1f, 10f);
            rightBody.CreateFixture(rightShape);

            Body rampBody = BodyFactory.CreateBody(_world, new Vector2(6f, -2f));
            PolygonShape rampShape = new PolygonShape(1f);
            rampShape.SetAsBox(5.5f, 0.5f);
            rampBody.CreateFixture(rampShape);
            rampBody.Rotation = -0.25f;

            Body circleBody = BodyFactory.CreateBody(_world, new Vector2(0, -0.2f));
            CircleShape circleShape = new CircleShape(2f, 1f);
            circleBody.CreateFixture(circleShape);
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _debugFont = Content.Load<SpriteFont>("debug");
        }

        protected override void UnloadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            _world.Step(DT);

            _fluidSimulation.update();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _fluidSimulation.draw();
            _debugView.RenderDebugData(ref _projection, ref _view);

            base.Draw(gameTime);
        }
    }
}
