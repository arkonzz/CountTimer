namespace CountTimer.Model
{
    /// <summary>事件重复方式。数值即编辑界面下拉框的序号</summary>
    public enum RepeatKind
    {
        None = 0,
        Daily = 1,
        /// <summary>周一至周五；有法定节假日数据时按调休后的实际工作日算</summary>
        Weekdays = 2,
        Weekly = 3,
    }
}
