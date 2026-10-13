# 参与开发

1. Fork 仓库并创建分支，保持修改聚焦。
2. 按 README 在 Windows x64 上构建，运行 `--self-test` 和 `--chat-self-test`。
3. 修改交互时检查真实窗口行为；截图验证使用合成测试图，不上传私人桌面。
4. 修改 AI 连接时保持 Codex 的聊天隔离和工具禁用，不将模型输出当作命令执行。
5. 不提交 build、checks、个人配置、密钥、登录文件或聊天记录；.gitignore 已排除这些路径。
6. PowerShell 脚本涉及中文时使用 UTF-8 BOM，保持 Windows PowerShell 5.1 可用。C# 使用系统 .NET Framework 编译器支持的语法。

Pull Request 请说明问题、修改后的行为、验证方式及尚未验证的部分。素材替换须注明来源及使用权限。贡献代码采用本项目 MIT 许可。
