# 奶猴动作图集规范

运行程序会优先读取同目录下的 `monkey-atlas.png` 和 `monkey-actions.json`。
如果图集不存在，会自动回退到 `front.png`、`side.png`、`back.png`，所以旧版仍可启动。

## 图集布局

- 8 列 x 6 行，共 48 个格子
- 每格 512 x 512 像素，整图 4096 x 3072 像素
- 背景使用纯色抠图，最终 PNG 必须带透明通道
- 每行对应 `monkey-actions.json` 中的一个动作：
  - 第 0 行：idle，呼吸、眨眼、观察
  - 第 1 行：crawl，四肢交替侧爬
  - 第 2 行：climb，沿垂直边缘攀爬
  - 第 3 行：hang，倒挂和晃动
  - 第 4 行：jump，起跳、腾空、落地
  - 第 5 行：sleep，趴下或蜷缩睡觉

## 生成流程

使用三视图作为参考图，先生成 8 帧横向侧爬动作，再按同一角色设定生成其余五行。
生成后将各行整理到 8x6 图集，文件名固定为 `monkey-atlas.png`，放回 `Assets` 目录。

Image API CLI 使用本机环境变量 `OPENAI_API_KEY`，不要把 Key 写入项目文件或提交到 Git。
