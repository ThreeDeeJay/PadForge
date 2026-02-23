using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using PadForge.ViewModels;

namespace PadForge.Views
{
    public partial class PadPage : UserControl
    {
        /// <summary>
        /// Raised when the user clicks a controller element to start recording.
        /// The string argument is the TargetSettingName (e.g., "ButtonA", "LeftTrigger").
        /// </summary>
        public event EventHandler<string> ControllerElementRecordRequested;

        /// <summary>Tag → FrameworkElement lookup, built on first Loaded.</summary>
        private Dictionary<string, FrameworkElement> _taggedElements;

        /// <summary>Saved opacity values for non-bound elements (restored on MouseLeave).</summary>
        private readonly Dictionary<FrameworkElement, double> _savedOpacities = new();

        /// <summary>The element currently being flash-animated during Map All.</summary>
        private FrameworkElement _flashingElement;
        private Storyboard _flashStoryboard;

        public PadPage()
        {
            InitializeComponent();
            Loaded += PadPage_Loaded;
        }

        private void PadPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Build the tag → element dictionary by walking the visual tree.
            _taggedElements = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
            CollectTaggedElements(this);

            // Subscribe to DataContext changes to wire Map All flash animation.
            WireMapAllFlash();
        }

        // ─────────────────────────────────────────────
        //  Hover highlight
        // ─────────────────────────────────────────────

        private void ControllerElement_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement el)
            {
                // Save original opacity for non-bound elements so we can restore it.
                if (BindingOperations.GetBindingExpression(el, OpacityProperty) == null)
                    _savedOpacities[el] = el.Opacity;

                // SetCurrentValue changes the displayed value WITHOUT removing
                // the underlying binding — the binding stays attached.
                el.SetCurrentValue(OpacityProperty, 0.7);
            }
        }

        private void ControllerElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement el)
            {
                // Don't restore opacity if this element is currently flash-animated.
                if (el == _flashingElement)
                    return;

                var expr = BindingOperations.GetBindingExpression(el, OpacityProperty);
                if (expr != null)
                {
                    // Force the binding to re-read from its source.
                    expr.UpdateTarget();
                }
                else if (_savedOpacities.TryGetValue(el, out double orig))
                {
                    // Restore the original XAML-specified opacity (e.g. 0.15 for arrows).
                    el.SetCurrentValue(OpacityProperty, orig);
                    _savedOpacities.Remove(el);
                }
                else
                {
                    el.ClearValue(OpacityProperty);
                }
            }
        }

        // ─────────────────────────────────────────────
        //  Click-to-record
        // ─────────────────────────────────────────────

        private void ControllerElement_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string targetName && !string.IsNullOrEmpty(targetName))
            {
                ControllerElementRecordRequested?.Invoke(this, targetName);
            }
        }

        // ─────────────────────────────────────────────
        //  Motor test (click) + hover highlight
        // ─────────────────────────────────────────────

        private void Motor_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement el)
                el.Opacity = 0.7;
        }

        private void Motor_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement el)
                el.Opacity = 1.0;
        }

        private void LeftMotor_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PadViewModel padVm)
                padVm.FireTestLeftMotor();
        }

        private void RightMotor_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PadViewModel padVm)
                padVm.FireTestRightMotor();
        }

        // ─────────────────────────────────────────────
        //  Map All stop button
        // ─────────────────────────────────────────────

        private void MapAllStop_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is PadViewModel padVm)
                padVm.StopMapAll();
        }

        // ─────────────────────────────────────────────
        //  Map All flash animation
        // ─────────────────────────────────────────────

        private PadViewModel _currentPadVm;

        private void WireMapAllFlash()
        {
            // When DataContext changes (pad selection), re-subscribe.
            DataContextChanged += (s, e) =>
            {
                if (_currentPadVm != null)
                    _currentPadVm.PropertyChanged -= OnPadVmPropertyChanged;

                _currentPadVm = DataContext as PadViewModel;
                if (_currentPadVm != null)
                    _currentPadVm.PropertyChanged += OnPadVmPropertyChanged;
            };

            // Wire current DataContext.
            _currentPadVm = DataContext as PadViewModel;
            if (_currentPadVm != null)
                _currentPadVm.PropertyChanged += OnPadVmPropertyChanged;
        }

        private void OnPadVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PadViewModel.CurrentRecordingTarget))
                return;

            // Ensure we're on the UI thread (PropertyChanged may fire from any thread).
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnPadVmPropertyChanged(sender, e)));
                return;
            }

            var target = _currentPadVm?.CurrentRecordingTarget;
            StopFlash();

            if (!string.IsNullOrEmpty(target))
            {
                // Rebuild the element dictionary every time — the TabControl may have
                // recycled its content since the last collection, making old references stale.
                _taggedElements = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
                CollectTaggedElements(this);

                if (_taggedElements.TryGetValue(target, out var element))
                    StartFlash(element);
            }
        }

        private void StartFlash(FrameworkElement element)
        {
            _flashingElement = element;

            // Save original opacity for non-bound elements before animating.
            if (BindingOperations.GetBindingExpression(element, OpacityProperty) == null
                && !_savedOpacities.ContainsKey(element))
                _savedOpacities[element] = element.Opacity;

            var animation = new DoubleAnimation
            {
                From = 0.1,
                To = 0.7,
                Duration = TimeSpan.FromMilliseconds(400),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            _flashStoryboard = new Storyboard();
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
            _flashStoryboard.Children.Add(animation);
            _flashStoryboard.Begin();
        }

        private void StopFlash()
        {
            if (_flashStoryboard != null && _flashingElement != null)
            {
                _flashStoryboard.Remove();
                _flashStoryboard = null;

                // Restore opacity: binding → UpdateTarget, non-bound → saved value.
                var expr = BindingOperations.GetBindingExpression(_flashingElement, OpacityProperty);
                if (expr != null)
                {
                    expr.UpdateTarget();
                }
                else if (_savedOpacities.TryGetValue(_flashingElement, out double orig))
                {
                    _flashingElement.SetCurrentValue(OpacityProperty, orig);
                    _savedOpacities.Remove(_flashingElement);
                }
                else
                {
                    _flashingElement.ClearValue(OpacityProperty);
                }

                _flashingElement = null;
            }
        }

        // ─────────────────────────────────────────────
        //  Visual tree helper
        // ─────────────────────────────────────────────

        private void CollectTaggedElements(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Tag is string tag && !string.IsNullOrEmpty(tag))
                {
                    _taggedElements[tag] = fe;
                }
                CollectTaggedElements(child);
            }
        }
    }
}
