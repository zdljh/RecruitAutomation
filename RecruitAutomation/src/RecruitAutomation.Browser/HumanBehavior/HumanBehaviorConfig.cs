using System;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 人工行为模拟配置
    /// 所有参数可调整以适应不同场景
    /// </summary>
    public class HumanBehaviorConfig
    {
        /// <summary>
        /// 默认配置
        /// </summary>
        public static HumanBehaviorConfig Default => new();

        /// <summary>
        /// 快速模式（减少延迟）
        /// </summary>
        public static HumanBehaviorConfig Fast => new()
        {
            ClickDelayMin = 50,
            ClickDelayMax = 150,
            TypeDelayMin = 30,
            TypeDelayMax = 80,
            MouseMoveSteps = 5,
            MouseMoveDelayMin = 5,
            MouseMoveDelayMax = 15
        };

        /// <summary>
        /// 谨慎模式（更像真人）
        /// </summary>
        public static HumanBehaviorConfig Cautious => new()
        {
            ClickDelayMin = 300,
            ClickDelayMax = 800,
            TypeDelayMin = 100,
            TypeDelayMax = 250,
            MouseMoveSteps = 25,
            MouseMoveDelayMin = 15,
            MouseMoveDelayMax = 40,
            ScrollDelayMin = 500,
            ScrollDelayMax = 1500,
            ActionIntervalMin = 1000,
            ActionIntervalMax = 3000
        };

        #region 鼠标移动配置

        /// <summary>
        /// 鼠标移动步数（越多越平滑）
        /// </summary>
        public int MouseMoveSteps { get; set; } = 15;

        /// <summary>
        /// 每步移动最小延迟（毫秒）
        /// </summary>
        public int MouseMoveDelayMin { get; set; } = 10;

        /// <summary>
        /// 每步移动最大延迟（毫秒）
        /// </summary>
        public int MouseMoveDelayMax { get; set; } = 25;

        /// <summary>
        /// 鼠标轨迹随机偏移量（像素）
        /// </summary>
        public int MousePathDeviation { get; set; } = 10;

        /// <summary>
        /// 贝塞尔曲线控制点随机范围
        /// </summary>
        public double BezierControlPointRange { get; set; } = 0.3;

        #endregion

        #region 点击配置

        /// <summary>
        /// 点击前最小延迟（毫秒）
        /// </summary>
        public int ClickDelayMin { get; set; } = 100;

        /// <summary>
        /// 点击前最大延迟（毫秒）
        /// </summary>
        public int ClickDelayMax { get; set; } = 300;

        /// <summary>
        /// 鼠标按下持续最小时间（毫秒）
        /// </summary>
        public int ClickHoldMin { get; set; } = 50;

        /// <summary>
        /// 鼠标按下持续最大时间（毫秒）
        /// </summary>
        public int ClickHoldMax { get; set; } = 150;

        /// <summary>
        /// 点击位置随机偏移（像素）
        /// </summary>
        public int ClickPositionOffset { get; set; } = 3;

        #endregion

        #region 输入配置

        /// <summary>
        /// 字符输入最小间隔（毫秒）
        /// </summary>
        public int TypeDelayMin { get; set; } = 50;

        /// <summary>
        /// 字符输入最大间隔（毫秒）
        /// </summary>
        public int TypeDelayMax { get; set; } = 150;

        /// <summary>
        /// 输入错误概率（0-1）
        /// </summary>
        public double TypoRate { get; set; } = 0.02;

        /// <summary>
        /// 输入暂停概率（模拟思考）
        /// </summary>
        public double TypePauseRate { get; set; } = 0.05;

        /// <summary>
        /// 输入暂停时长（毫秒）
        /// </summary>
        public int TypePauseDuration { get; set; } = 500;

        #endregion

        #region 滚动配置

        /// <summary>
        /// 滚动最小延迟（毫秒）
        /// </summary>
        public int ScrollDelayMin { get; set; } = 200;

        /// <summary>
        /// 滚动最大延迟（毫秒）
        /// </summary>
        public int ScrollDelayMax { get; set; } = 600;

        /// <summary>
        /// 单次滚动最小距离（像素）
        /// </summary>
        public int ScrollDistanceMin { get; set; } = 100;

        /// <summary>
        /// 单次滚动最大距离（像素）
        /// </summary>
        public int ScrollDistanceMax { get; set; } = 400;

        /// <summary>
        /// 滚动步数
        /// </summary>
        public int ScrollSteps { get; set; } = 8;

        #endregion

        #region 操作节奏配置

        /// <summary>
        /// 操作间隔最小时间（毫秒）
        /// </summary>
        public int ActionIntervalMin { get; set; } = 500;

        /// <summary>
        /// 操作间隔最大时间（毫秒）
        /// </summary>
        public int ActionIntervalMax { get; set; } = 2000;

        /// <summary>
        /// 长暂停概率（模拟阅读/思考）
        /// </summary>
        public double LongPauseRate { get; set; } = 0.1;

        /// <summary>
        /// 长暂停时长（毫秒）
        /// </summary>
        public int LongPauseDuration { get; set; } = 3000;

        #endregion
    }
}
