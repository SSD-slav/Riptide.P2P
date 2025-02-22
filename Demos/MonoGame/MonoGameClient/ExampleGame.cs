﻿
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2022 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RiptideNetworking.Utils;
using System;

namespace RiptideDemos.RudpTransport.MonoGame.TestClient
{
    public class ExampleGame : Game
    {
        public static Texture2D Pixel { get; set; }

        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        public ExampleGame()
        {
            RiptideLogger.Initialize(Console.WriteLine, false);

            graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 800,
                PreferredBackBufferHeight = 600
            };
            graphics.ApplyChanges();

            IsMouseVisible = true;

            Exiting += (s, e) => NetworkManager.Client.Disconnect();

            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        }

        protected override void Initialize()
        {
            NetworkManager.Connect();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Player.List.TryGetValue(NetworkManager.Client.Id, out Player localPlayer))
                localPlayer.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            NetworkManager.Client.Tick();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();

            foreach (Player player in Player.List.Values)
                player.Draw(spriteBatch);

            spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
