using Avalonia;
using Avalonia.Controls;
using System;

namespace GL3
{
    public partial class MainWindow : Window
    {
        public Size InitSize { get; private set; }
        public Avalonia.Platform.Screen? InitScreen { get; private set; }
        public MainWindow()
        {
            InitializeComponent();
            SetupWindowGeometry();
            // Загружаем начальную сцену по умолчанию при старте приложения
            SwitchScene(new DefaultScene(), "OpenGL Application");
        }
        private void SetupWindowGeometry()
        {
            var currentScreen = Screens.ScreenFromWindow(this) ?? throw new InvalidOperationException("Primary display is missing");
            double minPhysicalSide = Math.Min(currentScreen.Bounds.Width * 0.9, currentScreen.Bounds.Height * 0.9);
            double logicalSide = minPhysicalSide / currentScreen.Scaling;
            InitScreen = currentScreen;
            this.Width = logicalSide;
            this.Height = logicalSide;
            InitSize = new Size(this.Width, this.Height);
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        private void SwitchScene(BaseScene? newScene, string sceneName)
        {
            if (newScene == null) return;

            Title = sceneName;

            // 1. Передаем контекст окна ДО генерации UI элементов
            newScene.ParentWindow = this;
            openGlCanvas.CurrentScene = newScene;

            // 2. Очищаем панель и загружаем кнопки сцены (включая Reset)
            leftPanel.Children.Clear();
            foreach (var control in newScene.GetLeftPanelControls())
            {
                leftPanel.Children.Add(control);
            }

            // 3. Обновляем текст в статус-баре
            statusLabel.Text = $"Loaded scene: {sceneName}";
        }
        private void OnMenuItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Header != null)
            {
                string sceneName = menuItem.Header.ToString()!;

                BaseScene? newScene = sceneName switch
                {
                    "Simple Triangle" => new SimpleTriangle(),
                    "Clear Coloring" => new ClearColoring(),
                    _ => null,
                };

                // Переключаем сцену через универсальный метод
                SwitchScene(newScene, sceneName);
            }
            //if (sender is MenuItem menuItem)
            //{
            //    BaseScene? newScene = null;
            //    newScene = menuItem.Header?.ToString() switch
            //    {
            //        "Simple Triangle" => new SimpleTriangle(),
            //        "Clear Coloring" => new ClearColoring(),
            //        _ => null,
            //    };
            //    if (newScene != null)
            //    {
            //        Title = menuItem.Header?.ToString();
            //        // Передаем контекст окна ДО генерации UI элементов панели!
            //        newScene.ParentWindow = this;
            //        openGlCanvas.CurrentScene = newScene;
            //        leftPanel.Children.Clear();
            //        foreach (var control in newScene.GetLeftPanelControls())
            //        {
            //            // Если внутри сцены крутят ползунки, заставляем OpenGlControl перерисовываться
            //            //if (control is Slider slider)
            //            //{
            //            //    slider.ValueChanged += (s, ev) => openGlCanvas.RequestNextFrameRendering();
            //            //}
            //            leftPanel.Children.Add(control);
            //        }
            //        statusLabel.Text = $"Loaded scene: {menuItem.Header}";
            //    }
            //}
        }
    }
}