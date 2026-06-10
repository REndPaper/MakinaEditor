using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using MakinaEditor.Models;
using MakinaEditor.ViewModels;

namespace MakinaEditor.Views;

public partial class ScenarioGraphView : UserControl
{
    private bool _isDragging;
    private Point _pointerOffset;
    private GraphNodeInfo? _draggedNode;

    public ScenarioGraphView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void AddFlow_Click(object? sender, RoutedEventArgs e)
    {
        var txt = this.FindControl<TextBox>("NewFlowNameTxt");
        if (txt != null && !string.IsNullOrWhiteSpace(txt.Text) && DataContext is ScenarioGraphViewModel vm)
        {
            vm.AddFlowNode(txt.Text);
            txt.Text = "";
        }
    }

    private void AddLink_Click(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("TargetScenarioCombo");
        if (combo != null && combo.SelectedItem is string scenarioName && DataContext is ScenarioGraphViewModel vm)
        {
            vm.AddLinkNode(scenarioName);
        }
    }

    private void AddConditional_Click(object? sender, RoutedEventArgs e)
    {
        var txt = this.FindControl<TextBox>("ConditionExprTxt");
        if (txt != null && DataContext is ScenarioGraphViewModel vm)
        {
            vm.AddConditionalNode(txt.Text ?? "");
            txt.Text = "";
        }
    }

    private void Node_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is GraphNodeInfo node)
        {
            var properties = e.GetCurrentPoint(this);
            if (properties.Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _draggedNode = node;
                _pointerOffset = e.GetPosition(border);
                e.Pointer.Capture(border);
                e.Handled = true;
            }
        }
    }

    private void Node_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && _draggedNode != null && sender is Border border)
        {
            var currentPos = e.GetPosition(this.Parent as Visual ?? this);
            double newX = Math.Max(0, currentPos.X - _pointerOffset.X);
            double newY = Math.Max(0, currentPos.Y - _pointerOffset.Y);

            // 10단위로 스냅하여 그리드 정렬 유도 (옵션)
            _draggedNode.X = Math.Round(newX / 10.0) * 10.0;
            _draggedNode.Y = Math.Round(newY / 10.0) * 10.0;

            e.Handled = true;
        }
    }

    private void Node_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedNode = null;
            e.Pointer.Capture(null);
            e.Handled = true;

            if (DataContext is ScenarioGraphViewModel vm)
            {
                vm.SaveNodePositions();
            }
        }
    }

    private void Node_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is GraphNodeInfo node && DataContext is ScenarioGraphViewModel vm)
        {
            if (node.Type == NodeType.Flow)
            {
                vm.NavigateToFlow(node.BindingId);
                e.Handled = true;
            }
        }
    }
}
