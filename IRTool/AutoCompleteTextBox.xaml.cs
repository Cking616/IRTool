﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IRTool
{
    public class AutoCompleteEntry
    {
        private string[] keywordStrings;
        private string displayString;

        public string[] KeywordStrings
        {
            get
            {
                if (keywordStrings == null)
                {
                    keywordStrings = new string[] { displayString };
                }
                return keywordStrings;
            }
        }

        public string DisplayName
        {
            get { return displayString; }
            set { displayString = value; }
        }

        public AutoCompleteEntry(string name, params string[] keywords)
        {
            displayString = name;
            keywordStrings = keywords;
        }

        public override string ToString()
        {
            return displayString;
        }
    }

    /// <summary>
    /// AutoCompleteTextBox.xaml 的交互逻辑
    /// </summary>
    public partial class AutoCompleteTextBox : Canvas
    {
        #region 成员变量

        private VisualCollection controls;
        private TextBox textBox;
        private ComboBox comboBox;
        private ObservableCollection<AutoCompleteEntry> autoCompletionList;
        private Timer keypressTimer;
        private delegate void TextChangedCallback();
        private bool insertText;
        private int delayTime;
        private int searchThreshold;

        #endregion 成员变量

        #region 构造函数

        public AutoCompleteTextBox()
        {
            controls = new VisualCollection(this);
            InitializeComponent();

            autoCompletionList = new ObservableCollection<AutoCompleteEntry>();
            searchThreshold = 0;        // default threshold to 2 char
            delayTime = 100;

            // set up the key press timer
            keypressTimer = new System.Timers.Timer();
            keypressTimer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);

            // set up the text box and the combo box
            comboBox = new ComboBox();
            comboBox.IsSynchronizedWithCurrentItem = true;
            comboBox.IsTabStop = false;
            Panel.SetZIndex(comboBox, -1);
            comboBox.SelectionChanged += new SelectionChangedEventHandler(comboBox_SelectionChanged);

            textBox = new TextBox();
            textBox.TextChanged += new TextChangedEventHandler(textBox_TextChanged);
            textBox.GotFocus += new RoutedEventHandler(textBox_GotFocus);
            textBox.KeyUp += new KeyEventHandler(textBox_KeyUp);
            textBox.KeyDown += new KeyEventHandler(textBox_KeyDown);
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            textBox.TextAlignment = TextAlignment.Center;
            textBox.FontSize = 18;

            controls.Add(comboBox);
            controls.Add(textBox);
        }

        #endregion 构造函数

        #region 成员方法

        public string Text
        {
            get { return textBox.Text; }
            set
            {
                insertText = true;
                textBox.Text = value;
            }
        }

        public int DelayTime
        {
            get { return delayTime; }
            set { delayTime = value; }
        }

        public int Threshold
        {
            get { return searchThreshold; }
            set { searchThreshold = value; }
        }

        /// <summary>
        /// 添加Item
        /// </summary>
        /// <param name="entry"></param>
        public void AddItem(AutoCompleteEntry entry)
        {
            foreach(var E in autoCompletionList)
            {
                if (E.DisplayName == entry.DisplayName)
                    return;
            }
            autoCompletionList.Add(entry);
        }

        /// <summary>
        /// 清空Item
        /// </summary>
        /// <param name="entry"></param>
        public void ClearItem()
        {
            autoCompletionList.Clear();
        }

        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null != comboBox.SelectedItem)
            {
                insertText = true;
                ComboBoxItem cbItem = (ComboBoxItem)comboBox.SelectedItem;
                textBox.Text = cbItem.Content.ToString();
            }
        }

        private void TextChanged()
        {
            try
            {
                comboBox.Items.Clear();
                if (textBox.Text.Length >= searchThreshold)
                {
                    foreach (AutoCompleteEntry entry in autoCompletionList)
                    {
                        foreach (string word in entry.KeywordStrings)
                        {
                            // string upWord = word.ToUpper();
                            // string upText = textBox.Text.ToUpper();
                            // if (upWord.Contains(upText))
                            if (word.StartsWith(textBox.Text, StringComparison.CurrentCultureIgnoreCase))
                            {
                                ComboBoxItem cbItem = new ComboBoxItem();
                                cbItem.Content = entry.ToString();
                                comboBox.Items.Add(cbItem);
                                break;
                            }
                            //if (word.StartsWith(textBox.Text, StringComparison.CurrentCultureIgnoreCase))
                            //{
                            //    ComboBoxItem cbItem = new ComboBoxItem();
                            //    cbItem.Content = entry.ToString();
                            //    comboBox.Items.Add(cbItem);
                            //    break;
                            //}
                        }
                    }
                    comboBox.IsDropDownOpen = comboBox.HasItems;
                }
                else
                {
                    comboBox.IsDropDownOpen = false;
                }
            }
            catch { }
        }

        private void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
        {
            keypressTimer.Stop();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                new TextChangedCallback(this.TextChanged));
        }

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // text was not typed, do nothing and consume the flag
            if (insertText == true) insertText = false;

            // if the delay time is set, delay handling of text changed
            else
            {
                if (delayTime > 0)
                {
                    keypressTimer.Interval = delayTime;
                    keypressTimer.Start();
                }
                else TextChanged();
            }
        }

        //获得焦点时
        public void textBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // text was not typed, do nothing and consume the flag
            if (insertText == true) insertText = false;

            // if the delay time is set, delay handling of text changed
            else
            {
                if (delayTime > 0)
                {
                    keypressTimer.Interval = delayTime;
                    keypressTimer.Start();
                }
                else TextChanged();
            }
        }

        public void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (textBox.IsInputMethodEnabled == true)
            {
                comboBox.IsDropDownOpen = false;
            }
        }

        /// <summary>
        /// 按向下按键时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && comboBox.IsDropDownOpen == true)
            {
                comboBox.Focus();
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            textBox.Arrange(new Rect(arrangeSize));
            comboBox.Arrange(new Rect(arrangeSize));
            return base.ArrangeOverride(arrangeSize);
        }

        protected override Visual GetVisualChild(int index)
        {
            return controls[index];
        }

        protected override int VisualChildrenCount
        {
            get { return controls.Count; }
        }

        #endregion 成员方法
    }
}
