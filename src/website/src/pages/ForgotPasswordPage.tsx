import { Button, Form, Input, Typography, message } from "antd";
import { MailOutlined } from "@ant-design/icons";
import api from "../services/api";
import { useNavigate } from "react-router-dom";

const { Title, Text } = Typography;

export default function ForgotPasswordPage() {
  const [form] = Form.useForm();
  const nav = useNavigate();

  const onFinish = async (values: any) => {
    const email = values.email.trim().toLowerCase();
    await api.post("/api/auth/forgot-password", { email });
    message.success("If an account exists, you will receive a reset link.");
    nav("/login", { replace: true });
  };

  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
      <div style={{ width: 420, maxWidth: "92%" }}>
        <Title level={3}>Forgot Password</Title>
        <Text type="secondary">Enter your email to receive a reset link.</Text>

        <Form form={form} layout="vertical" onFinish={onFinish} style={{ marginTop: 16 }}>
          <Form.Item
            label="Email"
            name="email"
            rules={[
              { required: true, message: "Email is required." },
              { type: "email", message: "Please enter a valid email address" }
            ]}
          >
            <Input prefix={<MailOutlined />} placeholder="Email" />
          </Form.Item>

          <Button type="primary" htmlType="submit" block>Send Reset Link</Button>
        </Form>
      </div>
    </div>
  );
}