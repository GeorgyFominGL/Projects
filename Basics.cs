using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GL3
{
    public interface IScene
    {
        // Вызывается СТРОГО внутри OnOpenGlInit контрола
        void Initialize();

        // Вызывается СТРОГО внутри OnOpenGlRender контрола
        void Render();

        // Вызывается СТРОГО внутри OnOpenGlDeinit или при смене сцены
        void CleanUp();
    }
    public abstract class BaseScene : IScene
    {
        // 1. Дефолтный цвет теперь общая константа для всей иерархии
        public const int DEFAULT_RED = 0;
        public const int DEFAULT_GREEN = 0;
        public const int DEFAULT_BLUE = 51;
        protected static Color4 ToColor4(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        protected static Color DefColor => Color.FromArgb(255, DEFAULT_RED, DEFAULT_GREEN, DEFAULT_BLUE);
        public static Brush DefBrush => new SolidColorBrush(DefColor);
        // Ссылка на окно для изменения его размеров и строки статуса
        // Устанавливается при создании сцены в MainWindow
        public Window? ParentWindow { get; set; }
        public abstract void Initialize();
        public abstract void Render();
        public abstract void CleanUp();
        /// <summary>
        /// Создает кнопку с заданной надписью и ориентацией надписи для левой и правой панелей
        /// </summary>
        /// <param name="text">Текст на кнопке</param>
        /// <param name="direction">-1 для левой панели и +1 для правой</param>
        /// <returns></returns>
        public static Button GetButton(string text, int direction) =>
            new()
            {
                Width = 90,
                Height = 90,
                Background = DefBrush,
                Foreground = Brushes.Yellow,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = text,
                    Width = 90,
                    Height = 90,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    RenderTransform = new RotateTransform(direction * 90)
                }
            };

        // Базовый метод для левой панели (создает вертикальную кнопку)
        public virtual List<Control> GetLeftPanelControls()
        {
            var controls = new List<Control>();

            // Создаем кнопку с вертикальным текстом
            var btnReset = GetButton("Reset", -1);
            btnReset.Click += (s, e) => ResetToDefault();
            controls.Add(btnReset);
            return controls;
        }
        public virtual List<Control> GetRightPanelControls() => [];
        //protected abstract void ResetToDefault();
        // Восстанавливает дефолтное положение и размеры окна
        protected virtual void ResetToDefault()
        {
            if (ParentWindow != null && ParentWindow is MainWindow mainWindow)
            {
                Size initSize = mainWindow.InitSize;
                ParentWindow.Width = initSize.Width;// DEFAULT_WIDTH;
                ParentWindow.Height = initSize.Height; //DEFAULT_HEIGHT;

                // Центрируем окно на экране
                // Вручную пересчитываем центр экрана, чтобы окно не уезжало
                var currentScreen = mainWindow.InitScreen;
                if (currentScreen != null)
                {
                    // Получаем центр экрана в физических пикселях
                    int screenCenterX = currentScreen.Bounds.X + (currentScreen.Bounds.Width / 2);
                    int screenCenterY = currentScreen.Bounds.Y + (currentScreen.Bounds.Height / 2);

                    // Переводим логические размеры нашего окна в физические пиксели
                    int windowHalfWidth = (int)((initSize.Width * currentScreen.Scaling) / 2);
                    int windowHalfHeight = (int)((initSize.Height * currentScreen.Scaling) / 2);

                    // Устанавливаем новую позицию (она задается в физических пикселях PixelPoint)
                    mainWindow.Position = new PixelPoint(screenCenterX - windowHalfWidth, screenCenterY - windowHalfHeight);
                }
                // 2. Универсально просим канвас перерисовать экран 
                // Это сработает для любой сцены, у которой есть ссылка на MainWindow!
                mainWindow.openGlCanvas.RequestNextFrameRendering();
            }
        }
    }
    public class DefaultScene : BaseScene
    {
        protected int basicShaderProgram;
        protected int vao;
        protected int vbo;
        public override void Initialize()
        {
            UpdateStatusText();
        }
        protected virtual void UpdateStatusText()
        {
            string status = $"Version: {GL.GetString(StringName.Version)}; " +
                            $"Renderer: {GL.GetString(StringName.Renderer)}";
            if (ParentWindow != null && ParentWindow is MainWindow mainWindow)
                mainWindow.statusLabel.Text = status;
        }

        public override void Render()
        {
            // Просто очищаем экран дефолтным цветом из BaseScene
            GL.ClearColor(ToColor4(DefColor));
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }

        public override void CleanUp()
        {
            if (vbo != 0) GL.DeleteBuffer(vbo);
            if (vao != 0) GL.DeleteVertexArray(vao);
            if (basicShaderProgram != 0) GL.DeleteProgram(basicShaderProgram);
        }
        /// <summary>
        /// Формирует VAO и загружает данные в VBO.
        /// </summary>
        internal protected virtual int GetVAO(Vector3[]? positions, params object[]? otherAttrs)
        {
            if (positions == null || positions.Length == 0) return 0;

            // 1. Генерируем и привязываем Vertex Array Object (VAO)
            int vaoId = GL.GenVertexArray();
            GL.BindVertexArray(vaoId);

            // 2. Генерируем и привязываем Vertex Buffer Object (VBO)
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            // 3. Загружаем массив координат Vector3 в видеопамять
            // OpenTK 4.x отлично понимает массивы структур без unsafe кода и вычисления размеров в байтах
            GL.BufferData(BufferTarget.ArrayBuffer, positions.Length * Vector3.SizeInBytes, positions, BufferUsageHint.StaticDraw);

            // Кроссплатформенный аналог NativeMethods.Finish() — встроенный метод GL.Finish()
            // (Рекомендуется убрать после отладки, так как он тормозит конвейер)
            //GL.Finish();

            // 4. Указываем OpenGL структуру данных в буфере (Шаг, который отсутствовал!)
            // layout(location = 0) соответствует первому параметру (0)
            // Vector3 состоит из 3 элементов типа Float. Данные не нормализованы (false).
            // Смещение между вершинами равно размеру Vector3 в байтах. Начинаем с отступа 0.
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            // Активируем атрибут под индексом 0 (координаты) в VAO
            GL.EnableVertexAttribArray(0);

            // 5. Отвязываем буферы (сбрасываем контекст), чтобы случайно не испортить их в других методах
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            // ВАЖНО: GL.DeleteBuffer(vbo) здесь БОЛЬШЕ НЕ ВЫЗЫВАЕТСЯ. Он перенесен в CloseScene!

            return vaoId;
        }
        /// <summary>
        /// Создает, компилирует и линкует программу шейдеров (метод стал нестатическим).
        /// </summary>
        protected static int GetShaderProgram(ShaderType[] shaderTypes, string[] shaderCodes)
        {
            int programId = GL.CreateProgram();

            // Массив для хранения ID скомпилированных шейдеров
            int[] shaderIds = new int[shaderTypes.Length];

            for (int i = 0; i < shaderTypes.Length; i++)
            {
                // Создаем шейдер
                shaderIds[i] = GL.CreateShader(shaderTypes[i]);

                string finalShaderCode = shaderCodes[i];

                // Автоматическая подстановка версии языка (ваша отличная идея, адаптированная под OpenTK 4)
                if (!finalShaderCode.Contains("#version"))
                {
                    finalShaderCode = $"#version 300 es\nprecision highp float;\n{finalShaderCode}";
                }

                // Передаем код шейдера (в OpenTK 4 это делается простой передачей одной строки string)
                GL.ShaderSource(shaderIds[i], finalShaderCode);

                GL.CompileShader(shaderIds[i]);

                // Проверяем ошибки компиляции шейдера
                GL.GetShader(shaderIds[i], ShaderParameter.CompileStatus, out int success);
                if (success == 0)
                {
                    string infoLog = GL.GetShaderInfoLog(shaderIds[i]);
                    throw new Exception($"Ошибка компиляции шейдера {shaderTypes[i]}: {infoLog}");
                }

                // Прикрепляем к программе
                GL.AttachShader(programId, shaderIds[i]);
            }

            // Линкуем программу
            GL.LinkProgram(programId);

            // Проверяем ошибки линковки программы
            GL.GetProgram(programId, GetProgramParameterName.LinkStatus, out int linkSuccess);
            if (linkSuccess == 0)
            {
                string infoLog = GL.GetProgramInfoLog(programId);
                throw new Exception($"Ошибка линковки шейдерной программы: {infoLog}");
            }

            // После успешной линковки отдельные объекты шейдеров можно удалить из памяти GPU
            foreach (var shaderId in shaderIds)
            {
                GL.DetachShader(programId, shaderId);
                GL.DeleteShader(shaderId);
            }

            return programId;
            // Метод GL.UseProgram(programId) лучше вызывать непосредственно в DrawScene перед отрисовкой!
        }
    }
    public class SimpleTriangle : DefaultScene
    {
        protected virtual Vector3[] VPositions => [
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3( 0.5f, -0.5f, 0.0f),
            new Vector3( 0.0f,  0.5f, 0.0f)
        ];
        public override void Initialize()
        {
            string[] shaderCodes = [
                Properties.Resources.vsVertexShader.Replace("\r", string.Empty),
                Properties.Resources.fsFragmentShader.Replace("\r", string.Empty)
            ];
            basicShaderProgram = GetShaderProgram([ShaderType.VertexShader, ShaderType.FragmentShader], shaderCodes);
        }
        protected virtual void DrawScene()
        {
            if (vao == 0) vao = GetVAO(VPositions);
            if (basicShaderProgram != 0) GL.UseProgram(basicShaderProgram);

            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

        }
        public override void Render()
        {
            // Очищаем экран дефолтным цветом из базового класса
            base.Render();
            DrawScene();
        }
        public override void CleanUp()
        {
        }
    }
    public class ClearColoring : DefaultScene
    {
        // Храним текущий цвет сцены (изначально дефолтный)
        private Color _currentColor = DefColor;
        //ColorPicker? picker;
        // Переопределяем генерацию UI панелей
        public override List<Control> GetLeftPanelControls()
        {
            // Вызываем базовый метод, чтобы получить кнопку "InitRandom" от родителя
            var controls = base.GetLeftPanelControls();
            ////// Добавляем к ней ползунок управления красным цветом
            //var label = new TextBlock { Text = "Red Background:", Foreground = Avalonia.Media.Brushes.White };
            //controls.Add(label);
            //_redSlider = new Slider { Minimum = 0, Maximum = 1, Value = _r, Width = 150 };
            //_redSlider.ValueChanged += (s, e) =>
            //{
            //    _r = (float)e.NewValue;
            //    GL.ClearColor(ToColor4(Color.FromArgb(255, (byte)(int)(255 * _r), DEFAULT_GREEN, DEFAULT_BLUE)));
            //    Render();
            //};
            //controls.Add(_redSlider);
            var selectColorButton = GetButton("Clear Color", -1);
            selectColorButton.Click += async (s, e) => await PickCustomColorAsync();
            controls.Add(selectColorButton);

            return controls;
        }
        /// <summary>
        /// Метод запроса цвета. Вызывается при клике на кнопку из UI
        /// </summary>
        public async Task PickCustomColorAsync()
        {
            // Запрашиваем у UI выбор цвета, передавая текущий как стартовый
            Color? selectedColor = await RequestColorSelectionAsync(_currentColor);
            // Если пользователь выбрал цвет (не нажал отмену)
            if (selectedColor.HasValue)
            {
                _currentColor = selectedColor.Value;
            }
        }

        async Task<Color?> RequestColorSelectionAsync(Color initialColor)
        {
            if (ParentWindow is MainWindow mainWindow)
            {
                // Передаем лямбду: при каждом сдвиге ползунка (newColor) вызывается ваш метод
                var dialog = new ColorPickerDialog(initialColor, DefBrush, (newColor) =>
                {
                    // ВАЖНО: Если ваш старый метод Picker_ColorChanged требовал объект пикера,
                    // мы можем вызвать его здесь, либо просто выполнить логику обновления цвета сцены:

                    // Вариант А: Если вы можете переписать логику обновления цвета прямо сюда:
                    // MyScene.Background = new SolidColorBrush(newColor); 

                    // Вариант Б: Если нужно вызвать именно ваш старый метод Picker_ColorChanged,
                    // но у него внутри использовалось поле 'picker', временно подмените его или передайте цвет:
                    _currentColor = newColor;
                    // Запрашиваем перерисовку кадра
                    mainWindow.openGlCanvas.RequestNextFrameRendering();

                });
                await dialog.ShowDialog(mainWindow);
                return dialog.SelectedColor;
            }

            return null;
        }
        public override void Render()
        {
            // Очищаем экран дефолтным цветом из базового класса
            GL.ClearColor(ToColor4(_currentColor));
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }
        // Реализуем сброс конкретно для этой сцены
        protected override void ResetToDefault()
        {
            // Сбрасываем переменную цвета в коде
            _currentColor = DefColor;
            base.ResetToDefault();
        }
    }
    public class ColorPickerDialog : Window
    {
        private readonly ColorPicker _picker;
        public Color? SelectedColor { get; private set; }

        // Третий параметр теперь — простой Action, возвращающий Color на каждый сдвиг движка
        public ColorPickerDialog(Color initialColor, IBrush backgroundBrush, Action<Color>? onColorChanged = null)
        {
            Title = "Clear Color";
            Width = 350;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            _picker = new ColorPicker
            {
                Color = initialColor,
                IsAlphaEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Если сцена передала действие, вызываем его при каждом изменении ползунка
            if (onColorChanged != null)
            {
                _picker.ColorChanged += (s, e) =>
                {
                    onColorChanged(_picker.Color); // Передаем цвет «на лету» в сцену
                };
            }

            var mainStack = new StackPanel { Background = backgroundBrush, Margin = new Thickness(15), Spacing = 10 };
            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };

            var okButton = new Button { Foreground = Brushes.Yellow, Background = backgroundBrush, Content = "ОК", Width = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelButton = new Button { Foreground = Brushes.Yellow, Background = backgroundBrush, Content = "Отмена", Width = 80, HorizontalContentAlignment = HorizontalAlignment.Center };

            okButton.Click += (s, e) => { SelectedColor = _picker.Color; Close(); };
            cancelButton.Click += (s, e) => Close();

            buttonStack.Children.Add(okButton);
            buttonStack.Children.Add(cancelButton);
            mainStack.Children.Add(_picker);
            mainStack.Children.Add(buttonStack);

            Content = mainStack;
        }
    }

}
