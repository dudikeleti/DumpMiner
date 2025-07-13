using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DumpMiner.Common;

namespace DumpMiner.Contents
{
    public partial class OperationHelp : Window
    {
        private readonly IHelpService _helpService;
        private readonly string _operationName;
        private OperationHelpContent _helpContent;
        private Action<string> _askAICallback;

        public OperationHelp(string operationName, Action<string> askAICallback = null)
        {
            InitializeComponent();
            _operationName = operationName;
            _askAICallback = askAICallback;
            _helpService = App.Container.GetExportedValueOrDefault<IHelpService>() ?? new HelpService();
            
            LoadHelpContent();
        }

        private void LoadHelpContent()
        {
            _helpContent = _helpService.GetOperationHelp(_operationName);
            TitleTextBlock.Text = $"Help: {_helpContent.DisplayName}";
            
            BuildHelpUI();
        }

        private void BuildHelpUI()
        {
            ContentPanel.Children.Clear();

            // Operation Description
            AddHeader("📋 Description");
            AddText(_helpContent.Description);

            // WinDbg Equivalent
            if (!string.IsNullOrEmpty(_helpContent.WinDbgEquivalent))
            {
                AddHeader("🔧 WinDbg Equivalent");
                AddCode(_helpContent.WinDbgEquivalent);
            }

            // When to Use
            if (!string.IsNullOrEmpty(_helpContent.WhenToUse))
            {
                AddHeader("🎯 When to Use");
                AddText(_helpContent.WhenToUse);
            }

            // Parameters
            if (_helpContent.Parameters?.Any() == true)
            {
                AddHeader("⚙️ Parameters");
                foreach (var param in _helpContent.Parameters)
                {
                    var paramPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 10) };
                    
                    var paramHeader = new TextBlock
                    {
                        Text = $"{param.Name}" + (param.IsRequired ? " (Required)" : " (Optional)"),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12
                    };
                    
                    // Set foreground color using proper theme resources
                    if (param.IsRequired)
                    {
                        var errorBrush = TryFindResource("ValidationErrorElement") as SolidColorBrush;
                        paramHeader.Foreground = errorBrush ?? Brushes.Red;
                    }
                    else
                    {
                        var accentBrush = TryFindResource("Accent") as SolidColorBrush;
                        paramHeader.Foreground = accentBrush ?? Brushes.Blue;
                    }
                    paramPanel.Children.Add(paramHeader);
                    
                    if (!string.IsNullOrEmpty(param.Description))
                    {
                        paramPanel.Children.Add(new TextBlock 
                        { 
                            Text = param.Description, 
                            Style = (Style)FindResource("HelpTextStyle"),
                            Margin = new Thickness(10, 2, 0, 2)
                        });
                    }
                    
                    if (!string.IsNullOrEmpty(param.ExampleValue))
                    {
                        paramPanel.Children.Add(new TextBlock 
                        { 
                            Text = $"Example: {param.ExampleValue}", 
                            Style = (Style)FindResource("CodeStyle"),
                            Margin = new Thickness(10, 2, 0, 2)
                        });
                    }
                    
                    if (!string.IsNullOrEmpty(param.Notes))
                    {
                        paramPanel.Children.Add(new TextBlock 
                        { 
                            Text = $"Note: {param.Notes}", 
                            Style = (Style)FindResource("HelpTextStyle"),
                            Margin = new Thickness(10, 2, 0, 2),
                            FontStyle = FontStyles.Italic
                        });
                    }
                    
                    ContentPanel.Children.Add(paramPanel);
                }
            }

            // Usage Examples
            if (_helpContent.Examples?.Any() == true)
            {
                AddHeader("💡 Usage Examples");
                foreach (var example in _helpContent.Examples)
                {
                    var exampleBorder = new Border { Style = (Style)FindResource("ExampleBorderStyle") };
                    var examplePanel = new StackPanel();
                    
                    var titleTextBlock = new TextBlock 
                    { 
                        Text = example.Title, 
                        FontWeight = FontWeights.Bold,
                        FontSize = 13
                    };
                    
                    // Set foreground color safely
                    var accentBrush = TryFindResource("Accent") as SolidColorBrush;
                    titleTextBlock.Foreground = accentBrush ?? Brushes.DarkBlue;
                    
                    examplePanel.Children.Add(titleTextBlock);
                    
                    if (!string.IsNullOrEmpty(example.Scenario))
                    {
                        examplePanel.Children.Add(new TextBlock 
                        { 
                            Text = $"Scenario: {example.Scenario}", 
                            Style = (Style)FindResource("HelpTextStyle"),
                            Margin = new Thickness(0, 3, 0, 3)
                        });
                    }
                    
                    if (example.Steps?.Any() == true)
                    {
                        var stepsHeader = new TextBlock 
                        { 
                            Text = "Steps:", 
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 5, 0, 3)
                        };
                        
                        // Set theme-aware text color for better visibility
                        var textBrush = TryFindResource("ItemText") as SolidColorBrush;
                        stepsHeader.Foreground = textBrush ?? Brushes.Black;
                        
                        examplePanel.Children.Add(stepsHeader);
                        
                        for (int i = 0; i < example.Steps.Count; i++)
                        {
                            examplePanel.Children.Add(new TextBlock 
                            { 
                                Text = $"{i + 1}. {example.Steps[i]}", 
                                Style = (Style)FindResource("HelpTextStyle"),
                                Margin = new Thickness(10, 2, 0, 2)
                            });
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(example.ExpectedOutput))
                    {
                        examplePanel.Children.Add(new TextBlock 
                        { 
                            Text = $"Expected Output: {example.ExpectedOutput}", 
                            Style = (Style)FindResource("HelpTextStyle"),
                            Margin = new Thickness(0, 5, 0, 3),
                            FontStyle = FontStyles.Italic
                        });
                    }
                    
                    if (!string.IsNullOrEmpty(example.AnalysisTips))
                    {
                        var tipsTextBlock = new TextBlock 
                        { 
                            Text = $"💡 Analysis Tips: {example.AnalysisTips}", 
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 5, 0, 3),
                            Padding = new Thickness(5),
                            LineHeight = 16
                        };
                        
                        // Set background color using proper theme resources
                        var backgroundBrush = TryFindResource("ItemBackground") as SolidColorBrush;
                        if (backgroundBrush == null)
                        {
                            // Fallback to a background color that works with both themes
                            backgroundBrush = SystemParameters.HighContrast ? Brushes.LightGray : 
                                             new SolidColorBrush(Color.FromRgb(248, 248, 248));
                        }
                        tipsTextBlock.Background = backgroundBrush;
                        
                        // Ensure text is visible in both light and dark themes - use stronger theme color
                        var textBrush = TryFindResource("Text") as SolidColorBrush;
                        if (textBrush != null)
                        {
                            tipsTextBlock.Foreground = textBrush;
                        }
                        else
                        {
                            // Strong fallback colors for better visibility
                            tipsTextBlock.Foreground = SystemParameters.HighContrast ? Brushes.Black : Brushes.Black;
                        }
                        
                        examplePanel.Children.Add(tipsTextBlock);
                    }
                    
                    exampleBorder.Child = examplePanel;
                    ContentPanel.Children.Add(exampleBorder);
                }
            }

            // Common Scenarios
            if (_helpContent.CommonScenarios?.Any() == true)
            {
                AddHeader("🔍 Common Scenarios");
                foreach (var scenario in _helpContent.CommonScenarios)
                {
                    AddBulletPoint(scenario);
                }
            }

            // Output Explanation
            if (!string.IsNullOrEmpty(_helpContent.OutputExplanation))
            {
                AddHeader("📊 Understanding the Output");
                AddText(_helpContent.OutputExplanation);
            }

            // Troubleshooting Tips
            if (_helpContent.TroubleshootingTips?.Any() == true)
            {
                AddHeader("🔧 Troubleshooting Tips");
                foreach (var tip in _helpContent.TroubleshootingTips)
                {
                    AddBulletPoint(tip);
                }
            }

            // Related Operations
            if (_helpContent.RelatedOperations?.Any() == true)
            {
                AddHeader("🔗 Related Operations");
                foreach (var related in _helpContent.RelatedOperations)
                {
                    AddBulletPoint(related + " - " + _helpService.GetOperationHelp(related).Description);
                }
            }

            // Performance Notes
            if (!string.IsNullOrEmpty(_helpContent.PerformanceNotes))
            {
                AddHeader("⚡ Performance Notes");
                AddText(_helpContent.PerformanceNotes);
            }

            // Best Practices
            if (_helpContent.BestPractices?.Any() == true)
            {
                AddHeader("✅ Best Practices");
                foreach (var practice in _helpContent.BestPractices)
                {
                    AddBulletPoint(practice);
                }
            }
        }

        private void AddHeader(string text)
        {
            ContentPanel.Children.Add(new TextBlock 
            { 
                Text = text, 
                Style = (Style)FindResource("HelpHeaderStyle") 
            });
        }

        private void AddSubHeader(string text)
        {
            ContentPanel.Children.Add(new TextBlock 
            { 
                Text = text, 
                Style = (Style)FindResource("HelpSubHeaderStyle") 
            });
        }

        private void AddText(string text)
        {
            ContentPanel.Children.Add(new TextBlock 
            { 
                Text = text, 
                Style = (Style)FindResource("HelpTextStyle") 
            });
        }

        private void AddCode(string text)
        {
            ContentPanel.Children.Add(new TextBlock 
            { 
                Text = text, 
                Style = (Style)FindResource("CodeStyle") 
            });
        }

        private void AddBulletPoint(string text)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            
            var bulletPoint = new TextBlock 
            { 
                Text = "• ", 
                FontSize = 12, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 5, 0)
            };
            
            // Set theme-aware text color for bullet points
            var textBrush = TryFindResource("ItemText") as SolidColorBrush;
            bulletPoint.Foreground = textBrush ?? Brushes.Black;
            
            panel.Children.Add(bulletPoint);
            panel.Children.Add(new TextBlock 
            { 
                Text = text, 
                Style = (Style)FindResource("HelpTextStyle"),
                Margin = new Thickness(0)
            });
            ContentPanel.Children.Add(panel);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AskAIButton_Click(object sender, RoutedEventArgs e)
        {
            if (_askAICallback != null)
            {
                var question = $"I need help with the {_helpContent.DisplayName} operation. Can you explain how to use it effectively and what I should look for in the results?";
                _askAICallback(question);
                Close();
            }
            else
            {
                MessageBox.Show("AI assistance is not available for this operation.", "AI Help", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
} 