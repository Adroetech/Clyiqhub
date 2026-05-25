import React, { useMemo, useState } from "react";
import { Button, Checkbox, Form, Input, Typography, message, Space, Alert } from "antd";
import { MailOutlined, LockOutlined, GoogleOutlined, WindowsOutlined, MailFilled } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { loginApi } from "../services/auth";
import "../styles/login.css";
import { Link as RouterLink } from "react-router-dom";

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
    const [topError, setTopError] = useState<string | null>(null);

    // watch form values for button enable/disable
    const watchAll = Form.useWatch([], form);

    // acceptance: enabled only when required fields populated + valid email
    const signInEnabled = useMemo(() => {
        const email = (form.getFieldValue("email") || "").trim();
        const password = form.getFieldValue("password") || "";
        const emailValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
        return email.length > 0 && password.length > 0 && emailValid;
    }, [watchAll]);

    const onFinish = async (values: LoginForm) => {
        setTopError(null);

        // acceptance: trim leading/trailing and case-insensitive
        const email = values.email.trim().toLowerCase();
        const password = values.password;
        const remember = !!values.remember;

        setLoading(true);
        try {
            await loginApi(email, password, remember);

            // success: redirect to dashboard
            nav("/dashboard", { replace: true });
        } catch (err: any) {
  const code = err?.response?.data?.errorCode;
  const serverMsg = err?.response?.data?.message;

  let msgText = "Login failed. Please try again.";

  if (code === "USER_NOT_FOUND") {
    msgText = "User does not exist with this email.";
  } else if (code === "INVALID_CREDENTIALS") {
    msgText = "The email or password is incorrect.";
  } else if (code === "TOO_MANY_ATTEMPTS") {
    msgText = "Too many attempts. Please try again later.";
  } else if (code === "ACCOUNT_PENDING") {
    msgText = "Your account is pending approval.";
  } else if (code === "ACCOUNT_DISABLED") {
    msgText = "Your account is disabled.";
  } else if (serverMsg) {
    msgText = serverMsg; // fallback if backend sends message
  }

  setTopError(msgText);
  message.error(msgText);
  
}
finally { setLoading(false); }
    }

    return (
        <div className="login-root">


            <div className="login-left">
                <img
                    className="login-illustration-img"
                    src="/login-illustration.png"
                    alt="Login illustration"
                />
            </div>






            <div className="login-right">
                <div className="login-card">
                    <Title level={5} className="login-brand">{import.meta.env.VITE_APP_NAME}</Title>
                    <Title level={3} className="login-title">ClyiqHub</Title>
                    <Title level={2} className="login-welcome">Welcome again!</Title>
                    <Title level={3} className="login-title">Sign In</Title>

                    {topError && (
                        <div className="login-error">
                            <Alert type="error" message={topError} showIcon />
                        </div>
                    )}

                    <Form<LoginForm>
                        form={form}
                        layout="vertical"
                        requiredMark={false}
                        onFinish={onFinish}
                        initialValues={{ remember: true }}
                    >
                        {/* Email */}
                        <Form.Item
                            name="email"
                            rules={[
                                { required: true, message: "Email is required." },
                                { type: "email", message: "Please enter a valid email address" }
                            ]}
                            normalize={(v) => (typeof v === "string" ? v.trimStart() : v)}
                        >
                            <Input
                                className="clyiq-input"
                                placeholder="Email *"
                                autoComplete="email"
                            />
                        </Form.Item>

                        {/* Password */}
                        <Form.Item
                            name="password"
                            rules={[{ required: true, message: "Password is required." }]}
                        >
                            <Input.Password
                                className="clyiq-input"
                                placeholder="Password *"
                                autoComplete="current-password"
                                visibilityToggle
                            />
                        </Form.Item>


                        <div className="login-row">
                            <Form.Item name="remember" valuePropName="checked" noStyle>
                                <Checkbox>Remember Me</Checkbox>
                            </Form.Item>

                            {/* placeholder - wire later */}
                            <RouterLink to="/forgot-password">Forgot Password ?</RouterLink>
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

                        {/* Social buttons - disabled (not in scope) */}
                        <Space className="login-social" size="middle">
                            <Button icon={<GoogleOutlined />} disabled />
                            <Button icon={<WindowsOutlined />} disabled />
                            <Button icon={<MailFilled />} disabled />
                        </Space>

                        <div className="login-footer">
                            <Text>Copyright © 2026 - Adroetechnologies</Text>
                        </div>
                    </Form>
                </div>
            </div>
        </div>
        
    );
}
