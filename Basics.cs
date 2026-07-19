using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DynamicData;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    internal abstract class BaseScene : IScene
    {
        #region Fields
        // 1. Дефолтный цвет
        public const int DEFAULT_RED = 0;
        public const int DEFAULT_GREEN = 0;
        public const int DEFAULT_BLUE = 51;
        #endregion
        #region Properties
        protected static Color DefColor => Color.FromArgb(255, DEFAULT_RED, DEFAULT_GREEN, DEFAULT_BLUE);
        public static Brush DefBrush => new SolidColorBrush(DefColor);
        protected static Color4 ToColor4(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        // Ссылка на окно для изменения его размеров и строки статуса
        // Устанавливается при создании сцены в MainWindow
        public Window? ParentWindow { get; set; }
        #endregion
        public abstract void Initialize();
        public abstract void Render();
        public abstract void CleanUp();
        /// <summary>
        /// Создает кнопку с заданной надписью
        /// </summary>
        /// <param name="text">Текст на кнопке</param>
        /// <param name="panel">Панель, на которой кнопка размещается</param>
        /// <returns></returns>
        public static Button GetButton(string text, StackPanel panel)
        {
            // 1. Создаем текстовый блок с текстом
            var textBlock = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 2. Создаем контейнер LayoutTransformControl и помещаем текст в него
            var transformContainer = new LayoutTransformControl
            {
                LayoutTransform = new RotateTransform(-90), // Поворот макета на -90 градусов
                Child = textBlock // Передаем текст внутрь контейнера
            };

            // 3. Создаем саму кнопку оптимальных размеров
            var button = new Button
            {
                Background = DefBrush,
                Foreground = Brushes.Yellow,
                // Помещаем НАШ КОНТЕЙНЕР со вложенным текстом в контент кнопки
                Content = transformContainer,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinWidth = panel.Width,
                // ВАЖНО: Прижимаем кнопку по центру на StackPanel, чтобы панель не растягивала её на весь экран
                HorizontalAlignment = HorizontalAlignment.Center
            };
            return button;
        }
        // Базовый метод для левой панели (создает вертикальную кнопку)
        public virtual List<Control> GetLeftPanelControls()
        {
            var controls = new List<Control>();
            // Создаем кнопку с вертикальным текстом
            var btnReset = GetButton("Reset", (ParentWindow as MainWindow)!.leftPanel);
            btnReset.Click += (s, e) => ResetToDefault();
            controls.Add(btnReset);
            return controls;
        }
        public virtual List<Control> GetRightPanelControls() => [];
        // Восстанавливает дефолтное положение и размеры окна
        private static void ResetWindow(MainWindow mainWindow)
        {
            {
                Size initSize = mainWindow.InitSize;
                mainWindow.Width = initSize.Width;// DEFAULT_WIDTH;
                mainWindow.Height = initSize.Height; //DEFAULT_HEIGHT;

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
        /// <summary>
        /// Восстанавивает параметры по умолчанию
        /// </summary>
        protected virtual void ResetToDefault()
        {
            if (ParentWindow != null && ParentWindow is MainWindow mainWindow)
                ResetWindow(mainWindow);
        }
    }
    internal class DefaultScene : BaseScene
    {
        protected int basicShaderProgram;
        protected int vao;
        protected int vbo;
        public override void Initialize()
        {
            // Готовим шейдеры и массивы вершин, с которыми эти шейдеры работают
            PreRender();
            // Обновляем строку статуса
            UpdateStatusText();
        }
        protected virtual void PreRender()
        {
        }
        protected virtual void UpdateStatusText()
        {
            if (ParentWindow == null || ParentWindow is not MainWindow mainWindow) return;
            string status =
                $"Version: {GL.GetString(StringName.Version)}; " + $"Renderer: {GL.GetString(StringName.Renderer)}";
            // Гарантируем, что текст в строку статуса запишется внутри потока UI Avalonia
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                mainWindow.statusLabel.Text = status;
            });
        }
        public override void Render()
        {
            // Просто очищаем экран дефолтным цветом из BaseScene
            GL.ClearColor(ToColor4(DefColor));
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }

        public override void CleanUp()
        {
            // Удаляем ВСЕ созданные в этой сцене буферы
            if (vbo != 0)
            {
                GL.DeleteBuffer(vbo);
                vbo = 0;
            }
            if (vao != 0)
            {
                GL.DeleteVertexArray(vao);
                vao = 0;
            }
            if (basicShaderProgram != 0)
            {
                GL.DeleteProgram(basicShaderProgram);
                basicShaderProgram = 0;
            }
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
                // Автоматическая подстановка версии языка(ваша отличная идея, адаптированная под OpenTK 4)
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
    internal class ColorPickerDialog : Window
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
    internal class ClearColoring : DefaultScene
    {
        // Храним текущий цвет сцены (изначально дефолтный)
        private Color _currentColor = DefColor;
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
            var selectColorButton = GetButton("Clear Color", (ParentWindow as MainWindow)!.leftPanel);
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
    internal class SimpleTriangle : ClearColoring
    {
        protected virtual Vector3[] VPositions => [
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3( 0.5f, -0.5f, 0.0f),
            new Vector3( 0.0f,  0.5f, 0.0f)
        ];
        /// <summary>
        /// Хранит массив двух типов шейдеров - вершинного и шейдера фрагментов.
        /// </summary>
        protected ShaderType[] vfTypes = [ShaderType.VertexShader, ShaderType.FragmentShader];
        protected virtual string[] ShaderCodes => [
            Properties.Resources.vsVertexShader.Replace("\r", string.Empty),
            Properties.Resources.fsFragmentShader.Replace("\r", string.Empty)
        ];
        protected override void PreRender()
        {
            // Формируем и активируем программу шейдеров.
            // В метод GetShaderProgram передается два параметра:
            // массив типов шейдеров, из которых состоит программа и массив строк с содержимым отдельных щейдеров. 
            // Строка каждого шейдера - это текст программы.
            // Элементы массива - строки шейдеров, входящих в программу.
            // Этими строками в данном случае являются текстовые файлы ресурсов под именем vsVertexShader и fsFragmentShader.
            // Метод возвращает имя созданной программы.
            basicShaderProgram = GetShaderProgram(vfTypes, ShaderCodes);
            // Устанавливает объекты массивов вершин моделей, используемых в сцене.
            SetModels();
            // Устанавливаем программу как часть состояния воспроизведения
            GL.UseProgram(basicShaderProgram);
        }

        protected virtual void SetModels() =>
            // Готовим объект массива положений вершин.
            vao = GetVAO(VPositions);

        protected virtual void DrawScene()
        {
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
    }
    /// <summary>
    /// Добавляет атрибут цвета к вершинам.
    /// </summary>
    internal class Coloring : SimpleTriangle
    {
        // Заменяем одиночное поле vbo на список для предотвращения утечек памяти
        protected readonly List<int> vbos = [];
        #region Properties
        /// <summary>
        /// Возвращает цвета вершин.
        /// </summary>
        /// <remarks>
        /// Компоненты вектора цвета являются интенсивностями красного,зеленого и синего цвета в интервале [0; 1].
        /// </remarks>
        protected virtual Vector3[] VColors =>
        [
            //new Vector3(0, 1, 1),
            //new Vector3(1, 0, 1), new Vector3(0, 1, 1),
            new Vector3(1, 1, 0), new Vector3(1, 0, 1), new Vector3(0, 1, 1) ];
        /// <summary>
        /// Возвращает коды шейдеров в составе программы.
        /// </summary>
        protected override string[] ShaderCodes => [
            Properties.Resources.vsColoring.Replace("\r", string.Empty),
            Properties.Resources.fsColoring.Replace("\r", string.Empty)
            ];

        #endregion
        public override void CleanUp()
        {
            // Освобождаем все занятые имена VBO. 
            GL.DeleteBuffers(vbos.Count, [.. vbos]);
            vbos.Clear();
            base.CleanUp();
        }
        #region PreRender
        /// <summary>
        /// Формирует объект массива вершин (VAO) и возвращает его имя.
        /// </summary>
        /// <param name="positions">Массив положений вершин.</param>
        /// <param name="otherAttrs">Массивы других атрибутов вершины.</param>
        /// <returns>Имя объекта массива вершин.</returns>
        internal protected override int GetVAO(Vector3[]? positions, params object[]? otherAttrs)
        {
            ArgumentNullException.ThrowIfNull(positions);
            // Вызываем унаследованный метод получения vaoId, если отсутствуют другие атрибуты кроме положений вершин
            if (otherAttrs == null) return base.GetVAO(positions);
            // Генерируем имя объекта массива вершин (Vertex Array Object, или vaoId).
            // 1. Генерируем и привязываем Vertex Array Object (VAO)
            int vaoId = GL.GenVertexArray();
            GL.BindVertexArray(vaoId);
            // Создаем список имен объектов буфера вершин.
            // 2. Генерируем и привязываем Vertex Buffer Object (VBO)
            int vboId = GL.GenBuffer();
            vbos.Add(vboId); // Регистрируем буфер в списке для удаления при очистке сцены
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);
            // 3. Загружаем массив координат Vector3 в видеопамять
            // OpenTK 4.x отлично понимает массивы структур без unsafe кода и вычисления размеров в байтах
            GL.BufferData(BufferTarget.ArrayBuffer, positions.Length * Vector3.SizeInBytes, positions, BufferUsageHint.StaticDraw);
            // Блокируем какие-либо действия до полной передачи информации в видеопамять.
            // После отладки можно убрать.
            //GL.Finish();

            // Добавляем атрибут положений вершин в качестве первого атрибута.
            AddVertexAttribToVAO(vaoId, "vertexPosition", 3);

            // Завершаем работу с объектом буфера положений вершин vboNames[0].
            // Освобождаем ссылку ArrayBuffer от объекта.
            //            GL.BindBuffer(GL._ArrayBuffer, 0);
            // 5. Отвязываем буферы (сбрасываем контекст), чтобы случайно не испортить их в других методах
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            // Созданный vaoId сохраняет свое состояние. 
            // К нему можно добавить новые vbo.

            // Добавляем к списку vbos другие атрибуты, если они существуют.
            if (otherAttrs.Length > 0)
                AddVBO(vaoId, otherAttrs);

            // Возвращаем готовую ссылку vaoId на объект массива вершин.
            return vaoId;
        }
        /// <summary>
        /// Добавляет ссылку на векторный атрибут вершин к объекту массива вершин.
        /// </summary>
        /// <param name="vaoId">Имя объекта массива вершин, к которому атрибут добавляется.</param>
        /// <param name="attrName">Имя атрибута в шейдере.</param>
        /// <param name="size">Число компонент в векторе атрибута.</param>
        protected void AddVertexAttribToVAO(int vaoId, string attrName, int size)
        {
            // Подключаем объект массива вершин к контексту OpenGL.
            GL.BindVertexArray(vaoId);
            uint
                // Для текущей программы шейдеров определяем индекс (локацию) атрибута вершин.
                location = (uint)GL.GetAttribLocation(basicShaderProgram, attrName);
            // Если атрибут не описан в шейдерах, метод gl.GetAttribLocation возвратит -1. Число типа int.
            // В представлении типа uint значение -1 равно uint.MaxValue (все бинарные единицы в 4-ех байтах).
            // Если атрибут описан в шейдерах, то location != uint.MaxValue.
            // Более сильное условие на location - оно должно быть меньше максимально допустимого.
            if (location < GL.GetInteger(GetPName.MaxVertexAttribs)) //или location != uint.MaxValue)
            // Активируем массив вершин, помеченный location.
            // Указываем, что каждый элемент массива содержит size компонент типа float.
            // Это могут быть элементы типа Vector3 (size = 3) или Vector2 (size = 2).
            {
                // Смещение между вершинами равно размеру Vector3 в байтах. Начинаем с отступа 0, stride = 0.
                GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, 0, 0);
                // Активируем атрибут под индексом location в VAO
                GL.EnableVertexAttribArray(location);
            }
            // Завершение работы с vaoId.
            // Освобождаем контекст OpenGL от связи с объектом массива вершин.
            GL.BindVertexArray(0);
        }
        /// <summary>
        /// Добавляет атрибут цвета к вершинам.
        /// </summary>
        /// <param name="vaoId">Имя объекта массивов вершин к которому добавляются атрибуты.</param>
        /// <param name="otherAttrs">Массив атрибутов вершин.</param>
        /// <remarks>
        /// Метод рассчитан на добавление любых атрибутов с расширением списка объектов буфера вершин в наследниках.
        /// </remarks>
        protected virtual void AddVBO(int vaoId, object[] otherAttrs)
        {
            // Сохраняем цвета.
            Vector3[] colors = (Vector3[])otherAttrs[0];
            // Если массив цветов не содержит элементов, метод не выполняется.
            if (colors.Length == 0)
                return;
            // Добавляем в имеющийся список имен объектов буфера вершин новое имя.
            vbos.Add(GL.GenBuffer());
            // Подключаем последний объект буфера вершин из списка vboNames к контексту OpenGL через ссылку ArrayBuffer.
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbos.Last());

            // По ссылке ArrayBuffer передаем данные в виде массива векторов цвета.
            GL.BufferData(BufferTarget.ArrayBuffer, colors.Length * Vector3.SizeInBytes, colors, BufferUsageHint.StaticDraw);
            // Блокируем какие-либо действия до полной передачи информации в видеопамять.
            // После отладки можно убрать.
            //GL.Finish();
            // Добавляем атрибут цвета.
            AddVertexAttribToVAO(vaoId, "vertexColor", 3);
            // Освобождаем ссылку ArrayBuffer от объекта.
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
        /// <summary>
        /// Устанавливает объект массива вершин с массивами положений и цветов модели.
        /// </summary>
        protected override void SetModels() =>
            // Готовим объект массивов положений и цветов вершин.
            vao = GetVAO(VPositions, VColors);
        #endregion
    }
    internal class Viewport : Coloring
    {
        private readonly int[] defaultViewport = new int[4];
        protected Button? viewportButton;
        protected ViewportForm viewportForm = new();
        private ViewportViewModel? viewportViewModel;
        public override void Initialize()
        {
            if (ParentWindow is not MainWindow mainWindow) return;
            // 1. СНАЧАЛА создаем вью-модель, чтобы базовый класс мог с ней работать
            viewportViewModel = new ViewportViewModel(mainWindow);
            mainWindow.openGlCanvas.RequestNextFrameRendering();

            // 2. Передаем контекст данных в форму
            viewportForm.DataContext = viewportViewModel;

            // 3. ТОЛЬКО ТЕПЕРЬ вызываем базовую инициализацию буферов и шейдеров
            base.Initialize();

            // 5. Подписываемся на события окон
            SubscribeToWindowEvents();
        }
        public override void Render()
        {
            if (viewportViewModel != null)
            {
                // Извлекаем базовые пиксельные размеры холста
                int baseWidth = defaultViewport[2];
                int baseHeight = defaultViewport[3];

                // Вычисляем новые пиксельные параметры на основе коэффициентов из VM (0.0 ... 1.0)
                int x = (int)(viewportViewModel.GlX * baseWidth);
                int y = (int)(viewportViewModel.GlY * baseHeight);
                int width = (int)(viewportViewModel.GlWidth * baseWidth);
                int height = (int)(viewportViewModel.GlHeight * baseHeight);

                // Устанавливаем обновленный вьюпорт перед отрисовкой объектов сцены
                GL.Viewport(x, y, width, height);
            }
            base.Render();
        }
        private IDisposable? _boundsSubscription; // Токен для отписки от Bounds

        private void SubscribeToWindowEvents()
        {
            if (ParentWindow is not MainWindow mainWindow) return;

            // Подписка на перемещения главного окна
            mainWindow.PositionChanged += MainWindow_Changed;
            mainWindow.Resized += MainWindow_Changed;
            mainWindow.ScalingChanged += MainWindow_Changed;
            mainWindow.Closing += MainWindow_Closing;

            // Следим за изменением размеров главного окна (сохраняем токен!)
            _boundsSubscription = mainWindow.GetObservable(Window.BoundsProperty)
                                            .Subscribe(bounds => RelocateSubForm());

            // ВМЕСТО mainWindow.Activated используем деактивацию САМОЙ формы.
            // Это каноничный способ: форма потеряла фокус -> сама спряталась.
            viewportForm.Deactivated += ViewportForm_Deactivated;
        }

        private void MainWindow_Closing(object? sender, WindowClosingEventArgs e) =>
            viewportForm.Close();


        private void MainWindow_Changed(object? sender, EventArgs e) => RelocateSubForm();

        private void ViewportForm_Deactivated(object? sender, EventArgs e) =>
            // Форма потеряла фокус (кликнули по главному окну) -> просто прячем её
            viewportForm.Hide();

        protected override void PreRender()
        {
            // Запоминаем дефолтные значения параметров порта
            GL.GetInteger(GetPName.Viewport, defaultViewport);
            base.PreRender();
        }

        // Переопределяем метод базового класса для генерации ПРАВОЙ панели
        public override List<Control> GetRightPanelControls()
        {
            var controls = base.GetRightPanelControls();

            // Создаем вертикальную кнопку в стиле вашей кнопки Reset
            viewportButton = GetButton("Viewport", (ParentWindow as MainWindow)!.rightPanel);

            // Подписываемся на клик для открытия формы параметров
            viewportButton.Click += ViewportButton_Click;

            controls.Add(viewportButton);
            return controls;
        }

        private void ViewportButton_Click(object? sender, EventArgs e) => OpenViewportFormAsync();

        // Открытие вспомогательной формы
        private void OpenViewportFormAsync()
        {
            if (ParentWindow is not MainWindow mainWindow) return;
            // Если форма уже открыта — переводим на неё фокус
            if (viewportForm.IsVisible)
            {
                viewportForm.Activate();
            }
            else
            {
                // Позиционируем форму рядом с главным окном перед показом
                RelocateSubForm();

                // Показываем форму независимо рядом
                viewportForm.Show(mainWindow);

                // Переводим фокус на неё, чтобы спиннеры были сразу активны
                viewportForm.Activate();
            }
        }

        private void RelocateSubForm()
        {
            if (ParentWindow is not MainWindow mainWindow
                || mainWindow.centerArea is null
                || !mainWindow.centerArea.IsEffectivelyVisible ||
                mainWindow.centerArea is not Avalonia.Visual areaVisual) return;
            // 1. Берем правый верхний угол центральной области (Ширина панели, Y = 0)
            var topRightLocal = new Point(mainWindow.centerArea.Bounds.Width, 0);

            // 2. Переводим эту локальную точку в абсолютные экранные координаты
            var screenPoint = areaVisual.PointToScreen(topRightLocal);

            // 3. Выравниваем вспомогательное окно:
            // Смещаем влево на ширину самого окна (_viewportWindow.Width) плюс небольшой зазор (например, 5px)
            int targetX = screenPoint.X - (int)viewportForm.Width - 5;

            // Опускаем чуть ниже границы меню (например, на 5px), чтобы окно не прилипало вплотную
            int targetY = screenPoint.Y + 1;

            // 4. Применяем координаты к окну
            viewportForm.Position = new PixelPoint(targetX, targetY);
        }

        protected override void ResetToDefault()
        {
            // Во ViewModel у нас уже есть написанный метод Reset(), используем его,
            // чтобы не дублировать код присвоения Width/Height/X/Y вручную
            viewportViewModel?.Reset();
            base.ResetToDefault();
        }

        public override void CleanUp()
        {
            // 1. Прячем форму в UI-потоке, НО НЕ ЗАКРЫВАЕМ (.Close не вызывать!)
            Avalonia.Threading.Dispatcher.UIThread.Post(viewportForm.Hide);

            // 2. Очищаем кнопку и отписываемся от клика
            if (viewportButton != null)
            {
                viewportButton.Click -= ViewportButton_Click;
                viewportButton = null;
            }

            // 3. Зеркально отписываемся от всех событий окон (защита от утечки памяти)
            if (ParentWindow is MainWindow mainWindow)
            {
                mainWindow.PositionChanged -= MainWindow_Changed;
                mainWindow.Resized -= MainWindow_Changed;
                mainWindow.ScalingChanged -= MainWindow_Changed;
                mainWindow.Closing -= MainWindow_Closing;
            }

            viewportForm.Deactivated -= ViewportForm_Deactivated;

            // Уничтожаем подписку на Bounds
            _boundsSubscription?.Dispose();
            _boundsSubscription = null;

            base.CleanUp();
        }
    }
    internal class Indexing : Viewport
    {
        #region Fields
        /// <summary>
        /// Хранит имя объекта массива вершин пирамиды.
        /// </summary>
        protected int pyramidVAO;
        /// <summary>
        /// Хранит цвета вершин пирамиды.
        /// </summary>
        protected Vector3[] pyramidVColors =
            [
                new(1, 1, 0), new(0, 1, 1),
                new(1, 0, 1), new(0, 0, 0)
            ];
        /// <summary>
        /// Хранит индексы граней пирамиды. 
        /// </summary>
        protected int[] pyramidFaceIndices = [
                // Указываем в направлении против часовой стрелки для боковых граней и по часовой стрелке для задней.
                0, 1, 3,
                1, 2, 3,
                2, 0, 3,
                1, 0, 2 ];
        #endregion
        #region Properties
        /// <summary>
        /// Возвращает вектора положений вершин пирамиды.
        /// </summary>
        protected virtual Vector3[] PyramidVPositions =>
            [
                // Положения вершин пирамиды указываем в произвольном порядке. 
                .99f * Vector3.One, .99f * new Vector3(-1, 1, 1),  .99f * new Vector3(0, -1, 1), -.99f * Vector3.UnitZ
            ];
        #endregion
        #region Вход/выход
        public override void Initialize()
        {
            base.Initialize();
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
        }
        public override void CleanUp()
        {
            // Отключаем тест глубины.
            GL.Disable(EnableCap.DepthTest);
            if (pyramidVAO != 0)
            {
                // Отключаем использование объекта массива вершин.
                GL.DeleteVertexArray(pyramidVAO);
                pyramidVAO = 0;
            }
            // Освобождаем унаследованные ресурсы.
            base.CleanUp();
        }
        #endregion
        #region PreRender
        /// <summary>
        /// Создает VAO модели с индексированными вершинами.
        /// </summary>
        /// <param name="positions">Положения вершин.</param>
        /// <param name="otherAttrs">Дополнительные атрибуты.</param>
        /// <returns>Имя объекта массива вершин.</returns>
        protected internal override int GetVAO(Vector3[]? positions, params object[]? otherAttrs)
        {
            ArgumentNullException.ThrowIfNull(positions);
            // Если дополнительные атрибуты отсутствуют, то вызываем унаследованный метод GetVAO. 
            if (otherAttrs == null || otherAttrs.Length == 0) return base.GetVAO(positions);
            // Определяем номер последнего элемента массива дополнительных атрибутов.
            int lastIndex = otherAttrs.Length - 1;
            // Если последний элемент не содержит индексов, то есть не является массивом типа uint[], 
            // то вызываем унаследованный метод GetVAO. 
            if (otherAttrs[lastIndex] is not int[] indices) return base.GetVAO(positions, otherAttrs);
            // В том случае, если последний элемент массива otherAttrs содержит индексы, то вызываем метод, добавляющий индексы.
            return AddEAO(
                // получаем vaoId до добавки массива индексов и подставляем в качестве первого аргумента
                base.GetVAO(positions, lastIndex == 0 ? null : [.. otherAttrs.Take(lastIndex)]),
                // подставляем вторым аргументом массив индексов, который следует добавить 
                indices);
        }
        /// <summary>
        /// Добавляет к объекту массива вершин массив индексов.
        /// </summary>
        /// <param name="vaoId">Исходный объект массива вершин.</param>
        /// <param name="indices">Массив индексов.</param>
        /// <returns>Ссылку на обновленный объект массива вершин.</returns>
        private static int AddEAO(int vaoId, int[] indices)
        {
            // В базе неиспользованных имен выбираем имя нового буфера-объекта (область памяти)
            // для размещения в нем индексов. Это Element Array Object (eao) - объект элементов массива.
            int eao = GL.GenBuffer();
            // Порядок применения методов Bind важен!!!
            // В начале связываем объект массива вершин с контекстом OpenGL.
            GL.BindVertexArray(vaoId);
            // Назначаем объекту eao ссылку в качестве буфера элементов массива ElementArrayBuffer. 
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eao);
            // Передаем по этой ссылке данные - массив индексов граней.
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);
            // Блокируем какие-либо действия до полной передачи информации в видеопамять.
            // После отладки можно убрать.
            //GL.Finish();
            // Освобождаем контекст OpenGL от связи с объектами eao и vaoId в том же порядке!!!
            // В начале - от связи с объектом массива вершин vaoId.
            GL.BindVertexArray(0);
            // Затем освобождаем ссылку ElementArrayBuffer от связи с объектом элементов массива.
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            // Освобождаем базу имен от имени eao. 
            GL.DeleteBuffer(eao);
            // Возвращаем сформированный vaoId.
            return vaoId;
        }
        /// <summary>
        /// Устанавливает объекты массива вершин модели пирамиды с массивами положений, цветов и индексов.
        /// </summary>
        protected override void SetModels() =>
            // Готовим объекты массива вершин пирамиды с учетом индексации.
            pyramidVAO = GetVAO(PyramidVPositions, pyramidVColors, pyramidFaceIndices);
        #endregion
        public override void Render()
        {
            GL.Clear(ClearBufferMask.DepthBufferBit);
            base.Render();
        }
        /// <summary>
        /// Изображает пирамиду.
        /// </summary>
        protected override void DrawScene()
        {
            if (basicShaderProgram != 0) GL.UseProgram(basicShaderProgram);
            // Подключаем объект массива вершин пирамиды к контексту OpenGL.
            GL.BindVertexArray(pyramidVAO);
            // Формируем изображение пирамиды в вершинном шейдере и, далее, в шейдере фрагментов.
            GL.DrawElements(PrimitiveType.Triangles, pyramidFaceIndices.Length, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }
    }
    internal class Primitives : SimpleTriangle
    {
        // Вместо Form используем Window из Avalonia
        protected ModeForm modeForm = new();
        protected Button? pModeButton;

        // Ваши удобные векторы для геометрии
        protected override Vector3[] VPositions => [
            // Точка
            new Vector3(-0.6f, 0.6f, 0),
            // Линия
            new Vector3(-0.8f, -0.8f, 0), new Vector3(-0.8f, 0.8f, 0),
            // Треугольник (обход против часовой стрелки)
            new Vector3(-0.5f, 0.5f, 0), new Vector3(0, -0.5f, 0), new Vector3(.5f, .5f, 0)
        ];

        // Внутри класса Primitives:
        private PrimitivesViewModel? _viewModel;

        public override void Initialize()
        {
            // 1. СНАЧАЛА создаем вью-модель, чтобы базовый класс мог с ней работать
            _viewModel = new PrimitivesViewModel(() =>
            {
                if (ParentWindow is MainWindow mainWindow)
                    mainWindow.openGlCanvas.RequestNextFrameRendering();
            });

            // 2. Передаем контекст данных в форму
            modeForm.DataContext = _viewModel;

            // 3. ТОЛЬКО ТЕПЕРЬ вызываем базовую инициализацию буферов и шейдеров
            base.Initialize();

            // 4. БЕЗОПАСНО считываем начальные параметры из активного контекста GPU
            _viewModel.LoadFromOpenGL();

            // 5. Подписываемся на события окон
            SubscribeToWindowEvents();
        }
        // Переопределяем метод базового класса для генерации ПРАВОЙ панели
        public override List<Control> GetRightPanelControls()
        {
            var controls = base.GetRightPanelControls();

            // Создаем вертикальную кнопку в стиле вашей кнопки Reset
            pModeButton = GetButton("Primitive Modes", (ParentWindow as MainWindow)!.rightPanel);

            // Подписываемся на клик для открытия формы параметров
            pModeButton.Click += (s, e) => OpenModeFormAsync();

            controls.Add(pModeButton);
            return controls;
        }

        // Асинхронное открытие вспомогательной формы
        private void OpenModeFormAsync()
        {
            if (ParentWindow is MainWindow mainWindow)
            {
                // Если форма уже открыта, можно просто перевести на неё фокус
                if (modeForm.IsVisible)
                {
                    modeForm.Activate();
                }
                else
                {
                    // Позиционируем форму рядом с главным окном перед показом
                    RelocateSubForm();

                    // Показываем форму. Если нужно заблокировать главное окно, используем ShowDialog(mainWindow)
                    // Если форма должна висеть рядом независимо — просто modeForm.Show(mainWindow)
                    modeForm.Show(mainWindow);
                }
            }
        }
        //protected static void ToggleFormVisibility(Window form, Window parent)
        //{
        //    if (form.IsVisible)
        //    {
        //        form.Hide();
        //    }
        //    else
        //    {
        //        form.Show(parent);
        //    }
        //}
        // Метод, который мы вызовем при привязке окна к сцене
        private void SubscribeToWindowEvents()
        {
            if (ParentWindow is MainWindow mainWindow)
            {
                // Следим за перемещением главного окна
                mainWindow.PositionChanged += MainWindow_Changed;// MainWindow_PositionChanged;
                // Вариант А: Через событие изменения размера
                mainWindow.Resized += MainWindow_Changed; //MainWindow_Resized;
                mainWindow.ScalingChanged += MainWindow_Changed;
                // Следим за изменением размеров главного окна
                //mainWindow.GetObservable(Window.BoundsProperty).Subscribe(new Action<Rect>(bounds => RelocateSubForm());

                // Следим за активацией главного окна
                mainWindow.Activated += MainWindow_Activated;
            }
        }

        private void MainWindow_Changed(object? sender, EventArgs e)
        {
            RelocateSubForm();
        }
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Скрываем форму при активации главного окна (аналог HideSubForms)
            if (modeForm != null && modeForm.IsVisible)
            {
                modeForm.Hide();
            }
        }

        // Метод отслеживания положения окон (замена WinForms событий)
        private void RelocateSubForm()
        {
            if (ParentWindow is MainWindow mainWindow && modeForm != null)
            {
                // Логика вычисления координат (аналог вашей RelocateForms)
                // В Avalonia позиция задается через PixelPoint (физические пиксели)
                var mainPos = mainWindow.Position;
                var mainWidthInPixels = (int)(mainWindow.Bounds.Width * mainWindow.InitScreen!.Scaling);

                // Размещаем ModeForm вплотную справа от главного окна
                modeForm.Position = new PixelPoint(mainPos.X + mainWidthInPixels + 10, mainPos.Y);
            }
        }
        public override void CleanUp()
        {
            if (ParentWindow is MainWindow mainWindow)
            {
                // Обязательно отписываемся от событий, чтобы избежать утечек памяти
                mainWindow.PositionChanged -= MainWindow_Changed;
                mainWindow.Resized -= MainWindow_Changed;
                mainWindow.ScalingChanged -= MainWindow_Changed;
                mainWindow.Activated -= MainWindow_Activated;
            }

            // Вместо Close() просто скрываем окно и обнуляем DataContext
            if (modeForm != null)
            {
                modeForm.Hide();
                modeForm.DataContext = null;
            }
            // Закрываем форму параметров в UI-потоке
            //Avalonia.Threading.Dispatcher.UIThread.Post(() => modeForm?.Close());

            base.CleanUp();
        }
        protected override void ResetToDefault()
        {
            // 1. Сбрасываем настройки примитивов к начальным (ваша функция из класса GL)
            //            GL.SetInitMode();


            //// 2. Просим ViewModel перечитать новые (дефолтные) стейты из OpenGL
            //_viewModel?.LoadFromOpenGL();
            //// 2. Скрываем вспомогательную форму (в Avalonia это просто .Hide())
            //modeForm?.Hide();

            // 3. Просим форму ModeForm обновить свои ползунки/чекбоксы под дефолтные значения
            // (Когда напишем ModeForm, добавим сюда вызов, например:            modeForm.ResetControls();


            // 1. Сначала сбрасываем параметры во ViewModel чистыми числами (это безопасно для любых потоков)
            if (_viewModel != null)
            {
                _viewModel.PSize = 1.0m;
                _viewModel.LWidth = 1.0m;
                _viewModel.IsCullFaceEnabled = false;
                _viewModel.FaceModeIndex = 1; // CCW
            }

            // 2. Скрываем форму строго в UI-потоке Avalonia
            Avalonia.Threading.Dispatcher.UIThread.Post(() => modeForm?.Hide());

            // 4. Вызываем базовый метод, который обновит статус-бар и перерисует OpenGL-канвас
            base.ResetToDefault();
        }
        protected override void DrawScene()
        {
            if (basicShaderProgram == 0 || _viewModel == null) return;

            //GL.UseProgram(basicShaderProgram);

            // БЕЗОПАСНО: Синхронизируем стейт-машину OpenGL перед отрисовкой в графическом потоке
            //_viewModel.ApplyStatesToOpenGL(basicShaderProgram);

            GL.BindVertexArray(vao);

            // 1. Рисуем ТОЧКУ (индекс 0, количество 1)
            GL.DrawArrays(PrimitiveType.Points, 0, 1);

            // 2. Рисуем ЛИНИЮ (индексы 1 и 2, количество 2)
            GL.DrawArrays(PrimitiveType.Lines, 1, 2);

            // 3. Рисуем ТРЕУГОЛЬНИК (индексы 3, 4, 5, количество 3)
            GL.DrawArrays(PrimitiveType.Triangles, 3, 3);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
            //if (vaoId == 0) vaoId = GetVAO(VPositions); // Безопасная инициализация VAO, если забыли
            //if (basicShaderProgram != 0) GL.UseProgram(basicShaderProgram);

            //GL.BindVertexArray(vaoId);
            //// Отрисовка по вашим индексам векторов
            //GL.DrawArrays(PrimitiveType.Points, 0, 1);
            //GL.DrawArrays(PrimitiveType.Lines, 1, 2);
            //GL.DrawArrays(PrimitiveType.Triangles, 3, 3);

            //GL.BindVertexArray(0);
            //GL.UseProgram(0);
        }

    }
}
