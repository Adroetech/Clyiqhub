import React, { useMemo, useState } from "react";
import { Button, Checkbox, Form, Input, Typography, message, Space } from "antd";
import { MailOutlined, LockOutlined, GoogleOutlined, WindowsOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { loginApi } from "../services/auth";
import "../styles/login.css";

const { Title, Text, Link } = Typography;

type LoginForm = {
    email: string;
    password: string;
    remember: boolean;
};

export default function LoginPage() {
    const [form] = Form.useForm<LoginForm>();
    const nav = useNavigate();
    const [loading, setLoading] = useState(false);

    // Watch changes so we can enable/disable Sign In button
    const watchAll = Form.useWatch([], form);

    const signInEnabled = useMemo(() => {
        const email = (form.getFieldValue("email") || "").trim();
        const password = form.getFieldValue("password") || "";
        const emailValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
        return email.length > 0 && password.length > 0 && emailValid;
    }, [watchAll]);

    const onFinish = async (values: LoginForm) => {
        const email = values.email.trim().toLowerCase(); // trim + case-insensitive
        const password = values.password;

        setLoading(true);
        try {
            await loginApi(email, password, !!values.remember);
            nav("/dashboard", { replace: true });
        } catch (err: any) {
            const code = err?.response?.data?.errorCode;
            // Acceptance: invalid credentials message
            if (code === "INVALID_CREDENTIALS" || code === "USER_NOT_FOUND") {
                message.error("The email or password is incorrect.");
            } else {
                message.error("Login failed. Please try again.");
            }
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="login-root">
            <div className="login-left">
                <div className="login-illustration" />
            </div>

            <div className="login-right">
                <div className="login-card">
                    <Title level={5} className="login-brand">{import.meta.env.VITE_APP_NAME}</Title>
                    <Title level={2} className="login-welcome">Welcome again!</Title>
                    <Title level={3} className="login-title">Log In</Title>

                    <Form<LoginForm>
                        form={form}
                        layout="vertical"
                        requiredMark={false}
                        onFinish={onFinish}
                        initialValues={{ remember: true }}
                    >
                        <Form.Item
                            label="Email *"
                            name="email"
                            rules={[
                                { required: true, message: "Email is required." },
                                { type: "email", message: "Please enter a valid email address" }
                            ]}
                            // trim leading spaces while typing
                            normalize={(v) => (typeof v === "string" ? v.trimStart() : v)}
                        >
                            <Input prefix={<MailOutlined />} placeholder="Email" autoComplete="email" />
                        </Form.Item>

                        <Form.Item
                            label="Password *"
                            name="password"
                            rules={[{ required: true, message: "Password is required." }]}
                        >
                            <Input.Password
                                prefix={<LockOutlined />}
                                placeholder="Password"
                                autoComplete="current-password"
                                visibilityToggle // show/hide
                            />
                        </Form.Item>

                        <div className="login-row">
                            <Form.Item name="remember" valuePropName="checked" noStyle>
                                <Checkbox>Remember Me</Checkbox>
                            </Form.Item>

                            <Link onClick={(e) => e.preventDefault()}>Forgot Password ?</Link>
                        </div>

                        <Button
                            type="primary"
                            htmlType="submit"
                            block
                            loading={loading}
                            disabled={!signInEnabled}
                        >
                            Sign In
                        </Button>

                        <div className="login-or"><span>Or</span></div>

                        <Space className="login-social" size="middle">
                            <Button icon={<GoogleOutlined />} disabled />
                            <Button icon={<WindowsOutlined />} disabled />
                            <Button icon={<MailOutlined />} disabled />
                        </Space>

                        <div className="login-footer">
                            <Text type="secondary">Copyright © 2026 - Adroetechnologies</Text>
                        </div>
                    </Form>
                </div>
            </div>
        </div>
    );
}