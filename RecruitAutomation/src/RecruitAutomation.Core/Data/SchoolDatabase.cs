using System;
using System.Collections.Generic;
using System.Linq;

namespace RecruitAutomation.Core.Data
{
    /// <summary>
    /// 高校数据库
    /// 包含985/211/双一流/QS300院校名单
    /// </summary>
    public static class SchoolDatabase
    {
        /// <summary>
        /// 985院校（39所）
        /// </summary>
        public static readonly HashSet<string> Schools985 = new(StringComparer.OrdinalIgnoreCase)
        {
            "清华大学", "北京大学", "复旦大学", "上海交通大学", "浙江大学",
            "南京大学", "中国科学技术大学", "哈尔滨工业大学", "西安交通大学", "北京理工大学",
            "天津大学", "南开大学", "华中科技大学", "武汉大学", "中山大学",
            "厦门大学", "东南大学", "北京航空航天大学", "同济大学", "四川大学",
            "山东大学", "吉林大学", "大连理工大学", "西北工业大学", "中南大学",
            "华南理工大学", "电子科技大学", "重庆大学", "湖南大学", "兰州大学",
            "东北大学", "中国农业大学", "中国海洋大学", "西北农林科技大学", "中央民族大学",
            "华东师范大学", "国防科技大学", "北京师范大学", "中国人民大学"
        };

        /// <summary>
        /// 211院校（112所，包含985）
        /// </summary>
        public static readonly HashSet<string> Schools211 = new(StringComparer.OrdinalIgnoreCase)
        {
            // 985院校
            "清华大学", "北京大学", "复旦大学", "上海交通大学", "浙江大学",
            "南京大学", "中国科学技术大学", "哈尔滨工业大学", "西安交通大学", "北京理工大学",
            "天津大学", "南开大学", "华中科技大学", "武汉大学", "中山大学",
            "厦门大学", "东南大学", "北京航空航天大学", "同济大学", "四川大学",
            "山东大学", "吉林大学", "大连理工大学", "西北工业大学", "中南大学",
            "华南理工大学", "电子科技大学", "重庆大学", "湖南大学", "兰州大学",
            "东北大学", "中国农业大学", "中国海洋大学", "西北农林科技大学", "中央民族大学",
            "华东师范大学", "国防科技大学", "北京师范大学", "中国人民大学",
            // 其他211
            "北京交通大学", "北京工业大学", "北京科技大学", "北京化工大学", "北京邮电大学",
            "北京林业大学", "北京中医药大学", "北京外国语大学", "中国传媒大学", "中央财经大学",
            "对外经济贸易大学", "北京体育大学", "中国政法大学", "华北电力大学", "中国矿业大学",
            "中国石油大学", "中国地质大学", "上海财经大学", "上海外国语大学", "东华大学",
            "上海大学", "南京航空航天大学", "南京理工大学", "中国药科大学", "河海大学",
            "江南大学", "南京师范大学", "南京农业大学", "苏州大学", "合肥工业大学",
            "安徽大学", "福州大学", "南昌大学", "郑州大学", "武汉理工大学",
            "华中农业大学", "华中师范大学", "中南财经政法大学", "湖南师范大学", "暨南大学",
            "华南师范大学", "广西大学", "海南大学", "西南交通大学", "西南财经大学",
            "四川农业大学", "贵州大学", "云南大学", "西藏大学", "西北大学",
            "西安电子科技大学", "长安大学", "陕西师范大学", "青海大学", "宁夏大学",
            "新疆大学", "石河子大学", "哈尔滨工程大学", "东北农业大学", "东北林业大学",
            "延边大学", "东北师范大学", "辽宁大学", "大连海事大学", "太原理工大学",
            "内蒙古大学", "河北工业大学", "天津医科大学"
        };

        /// <summary>
        /// 双一流院校（部分）
        /// </summary>
        public static readonly HashSet<string> SchoolsDoubleFirstClass = new(StringComparer.OrdinalIgnoreCase)
        {
            // 包含所有985/211，加上新增的双一流
            "南方科技大学", "上海科技大学", "中国科学院大学", "首都师范大学",
            "外交学院", "中国人民公安大学", "北京协和医学院", "中国音乐学院",
            "中央美术学院", "中央戏剧学院", "天津工业大学", "天津中医药大学",
            "河北大学", "山西大学", "南京信息工程大学", "南京邮电大学",
            "南京林业大学", "南京医科大学", "南京中医药大学", "中国美术学院",
            "浙江工业大学", "浙江师范大学", "宁波大学", "广州医科大学",
            "广州中医药大学", "华南农业大学", "成都理工大学", "成都中医药大学",
            "西南石油大学", "西南大学", "湘潭大学"
        };

        /// <summary>
        /// QS世界排名前300的中国大学（部分）
        /// </summary>
        public static readonly HashSet<string> SchoolsQS300 = new(StringComparer.OrdinalIgnoreCase)
        {
            "清华大学", "北京大学", "复旦大学", "上海交通大学", "浙江大学",
            "中国科学技术大学", "南京大学", "武汉大学", "同济大学", "哈尔滨工业大学",
            "中山大学", "北京师范大学", "华中科技大学", "南开大学", "西安交通大学",
            "天津大学", "北京理工大学", "山东大学", "厦门大学", "东南大学",
            "华东师范大学", "北京航空航天大学", "吉林大学", "四川大学", "华南理工大学",
            "大连理工大学", "中南大学", "中国人民大学", "电子科技大学", "北京科技大学",
            "重庆大学", "湖南大学", "兰州大学", "上海大学", "南京理工大学",
            "苏州大学", "北京交通大学", "西北工业大学", "华东理工大学", "暨南大学",
            // 港澳台
            "香港大学", "香港科技大学", "香港中文大学", "香港城市大学", "香港理工大学",
            "台湾大学", "台湾清华大学", "台湾交通大学", "台湾成功大学", "澳门大学"
        };

        /// <summary>
        /// 海外QS300院校关键词
        /// </summary>
        public static readonly HashSet<string> OverseasQS300Keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "MIT", "Stanford", "Harvard", "Cambridge", "Oxford", "Caltech",
            "UCL", "Imperial", "ETH", "Chicago", "NUS", "NTU", "Princeton",
            "Cornell", "Yale", "Columbia", "Penn", "Michigan", "Duke",
            "Northwestern", "NYU", "UCLA", "Berkeley", "CMU", "Georgia Tech",
            "UIUC", "Wisconsin", "Toronto", "McGill", "Melbourne", "Sydney",
            "ANU", "Queensland", "UNSW", "Monash", "Edinburgh", "Manchester",
            "KCL", "LSE", "Warwick", "Bristol", "Glasgow", "Birmingham",
            "麻省理工", "斯坦福", "哈佛", "剑桥", "牛津", "加州理工",
            "伦敦大学学院", "帝国理工", "苏黎世联邦理工", "芝加哥大学",
            "新加坡国立", "南洋理工", "普林斯顿", "康奈尔", "耶鲁",
            "哥伦比亚", "宾夕法尼亚", "密歇根", "杜克", "西北大学",
            "纽约大学", "加州大学洛杉矶", "伯克利", "卡内基梅隆",
            "多伦多大学", "墨尔本大学", "悉尼大学", "爱丁堡大学"
        };

        /// <summary>
        /// 检查学校是否为985
        /// </summary>
        public static bool Is985(string schoolName)
        {
            if (string.IsNullOrWhiteSpace(schoolName)) return false;
            return Schools985.Any(s => schoolName.Contains(s) || s.Contains(schoolName));
        }

        /// <summary>
        /// 检查学校是否为211
        /// </summary>
        public static bool Is211(string schoolName)
        {
            if (string.IsNullOrWhiteSpace(schoolName)) return false;
            return Schools211.Any(s => schoolName.Contains(s) || s.Contains(schoolName));
        }

        /// <summary>
        /// 检查学校是否为双一流
        /// </summary>
        public static bool IsDoubleFirstClass(string schoolName)
        {
            if (string.IsNullOrWhiteSpace(schoolName)) return false;
            // 双一流包含所有985/211
            return Is985(schoolName) || Is211(schoolName) ||
                   SchoolsDoubleFirstClass.Any(s => schoolName.Contains(s) || s.Contains(schoolName));
        }

        /// <summary>
        /// 检查学校是否为QS300
        /// </summary>
        public static bool IsQS300(string schoolName)
        {
            if (string.IsNullOrWhiteSpace(schoolName)) return false;
            
            // 检查国内QS300
            if (SchoolsQS300.Any(s => schoolName.Contains(s) || s.Contains(schoolName)))
                return true;
            
            // 检查海外QS300关键词
            return OverseasQS300Keywords.Any(k => schoolName.Contains(k));
        }

        /// <summary>
        /// 获取学校标签
        /// </summary>
        public static List<string> GetSchoolTags(string schoolName)
        {
            var tags = new List<string>();
            if (Is985(schoolName)) tags.Add("985");
            if (Is211(schoolName)) tags.Add("211");
            if (IsDoubleFirstClass(schoolName)) tags.Add("双一流");
            if (IsQS300(schoolName)) tags.Add("QS300");
            return tags;
        }
    }
}
