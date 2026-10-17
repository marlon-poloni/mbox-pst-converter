import "./styles/fonts.css";
import "./styles/sistema.css";
import { mount } from "./app";
import { connect } from "./core";
import { services } from "./services";

const root = document.getElementById("app")!;
const [core, native] = await Promise.all([connect(), services()]);
mount(root, core, native);
