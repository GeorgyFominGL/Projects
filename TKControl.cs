using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using OpenTK.Graphics.OpenGL4;
using System;

namespace GL3
{
    public class AvaloniaGlBindingsContext : OpenTK.IBindingsContext
    {
        private readonly Func<string, IntPtr> _getProcAddress;

        public AvaloniaGlBindingsContext(Func<string, IntPtr> getProcAddress)
        {
            _getProcAddress = getProcAddress;
        }

        public IntPtr GetProcAddress(string procName) => _getProcAddress(procName);
    }
    internal class TKControl : OpenGlControlBase
    {
        private bool _openTkBindingsLoaded = false;
        private IScene? _currentScene;

        // Публичное свойство для управления сценой из MainWindow
        public IScene? CurrentScene
        {
            get => _currentScene;
            set
            {
                // 1. Если старая сцена существует, дожидаемся вызова её очистки.
                // Но делать GL вызовы можно только в потоке рендеринга Avalonia.
                // Поэтому безопаснее делать смену сцены через промежуточный запрос.
                _sceneToLoad = value;
                _shouldChangeScene = true;

                // Триггерим перерисовку, чтобы Avalonia вызвала OnOpenGlRender, 
                // где мы безопасно подменим сцену в контексте OpenGL
                RequestNextFrameRendering();
                //InvalidateVisual();
            }
        }

        private IScene? _sceneToLoad;
        private bool _shouldChangeScene = false;

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);
            if (!_openTkBindingsLoaded)
            {
                GL.LoadBindings(new AvaloniaGlBindingsContext(gl.GetProcAddress));
                _openTkBindingsLoaded = true;
            }
            _currentScene?.Initialize();
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            // Безопасная смена сцены ИМЕННО внутри контекста рендера
            if (_shouldChangeScene)
            {
                _currentScene?.CleanUp(); // Удаляем ресурсы старой сцены на GPU
                _currentScene = _sceneToLoad;
                _currentScene?.Initialize(); // Инициализируем ресурсы новой сцены на GPU
                _shouldChangeScene = false;
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb);
            GL.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);

            _currentScene?.Render();
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            _currentScene?.CleanUp();
            base.OnOpenGlDeinit(gl);
        }
    }
}
